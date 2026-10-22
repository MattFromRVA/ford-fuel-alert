using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FordFuelAlert.Functions.Services;

// Real fuel provider backed by the Smartcar Vehicle API v3 (https://smartcar.com/docs), selected when
// FuelAlert__UseFakeFuelService=false. Per run: get (or reuse) an application token via the client
// credentials grant, resolve the vehicle id, then read the fuel-level and range signals on behalf of
// the vehicle owner (sc-user-id). Every failure path degrades to null (logged warning) so a bad
// reading never crashes the timer run — the alert logic treats null as "no data".
public sealed class SmartcarFuelService : IFuelService
{
    private const string TokenUrl = "https://iam.smartcar.com/oauth2/token";
    private const string ApiBaseUrl = "https://vehicle.api.smartcar.com/v3";

    private const string FuelLevelSignal = "internalcombustionengine-fuellevel";
    private const string RangeSignal = "internalcombustionengine-range";

    private const double MilesPerKilometer = 0.621371;

    // Refresh a little before the token actually expires so an in-flight call can't race the boundary.
    private const int ExpiryBufferSeconds = 60;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SmartcarOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SmartcarFuelService> _logger;

    // Cached application token for the process lifetime. v3 issues no refresh token — a new one is
    // simply requested when this expires. The function host runs timer executions sequentially on
    // this singleton, so no locking is needed (same assumption as TableAlertStateService).
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresUtc;

    // Vehicle id discovered from the user's connection when Smartcar__VehicleId is not configured.
    private string? _discoveredVehicleId;

    public SmartcarFuelService(
        IHttpClientFactory httpClientFactory,
        IOptions<SmartcarOptions> options,
        TimeProvider timeProvider,
        ILogger<SmartcarFuelService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<FuelStatus?> GetFuelStatusAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.UserId))
        {
            _logger.LogWarning("Smartcar__UserId is not configured; cannot fetch fuel data.");
            return null;
        }

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        if (accessToken is null)
        {
            return null;
        }

        var vehicleId = await GetVehicleIdAsync(accessToken, cancellationToken);
        if (vehicleId is null)
        {
            return null;
        }

        // Fuel level decides the alert, so a failed read means no data at all.
        var fuelLevel = await GetSignalAsync(accessToken, vehicleId, FuelLevelSignal, cancellationToken);
        if (fuelLevel is null)
        {
            return null;
        }

        // Range is informational only (shown in the email), so a failed read keeps the percent.
        var range = await GetSignalAsync(accessToken, vehicleId, RangeSignal, cancellationToken);

        return new FuelStatus
        {
            PercentRemaining = MapPercent(fuelLevel),
            RangeMiles = range is null ? null : MapRangeMiles(range),
            RetrievedAtUtc = fuelLevel.Meta?.RetrievedAt ?? _timeProvider.GetUtcNow(),
            VehicleId = vehicleId,
        };
    }

    private async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        if (_accessToken is not null && now < _accessTokenExpiresUtc)
        {
            return _accessToken;
        }

        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            _logger.LogWarning("Smartcar client credentials are not configured; cannot fetch fuel data.");
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();

            // Client credentials grant, form-encoded with the credentials in the body (per Smartcar's
            // Request Access Token docs). The token is application-wide, not per vehicle.
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _options.ClientId),
                new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
            });

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Smartcar token request failed with status {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var token = JsonSerializer.Deserialize<TokenResponse>(body);
            if (token?.AccessToken is null)
            {
                _logger.LogWarning("Smartcar token request returned no access token.");
                return null;
            }

            _accessToken = token.AccessToken;
            _accessTokenExpiresUtc = now.AddSeconds(Math.Max(0, token.ExpiresIn - ExpiryBufferSeconds));
            return _accessToken;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient surfaces a request timeout as (Task)OperationCanceledException even when the
            // caller didn't cancel — treat that as unavailable data, but let real cancellation propagate.
            _logger.LogWarning("Smartcar token request timed out; treating fuel data as unavailable.");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
        {
            _logger.LogWarning(ex, "Smartcar token request errored; treating fuel data as unavailable.");
            return null;
        }
    }

    // Uses Smartcar__VehicleId when set; otherwise looks up the user's connections and uses the
    // vehicle if there is exactly one, so onboarding only has to capture the user id.
    private async Task<string?> GetVehicleIdAsync(string accessToken, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.VehicleId))
        {
            return _options.VehicleId;
        }

        if (_discoveredVehicleId is not null)
        {
            return _discoveredVehicleId;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();

            var url = $"{ApiBaseUrl}/connections?filter[userId]={Uri.EscapeDataString(_options.UserId!)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                InvalidateTokenIfUnauthorized(response);
                _logger.LogWarning("Smartcar connections request failed with status {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var vehicleIds = (JsonSerializer.Deserialize<ConnectionsResponse>(body)?.Data ?? [])
                .Select(connection => connection.Relationships?.Vehicle?.Data?.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (vehicleIds.Count != 1)
            {
                _logger.LogWarning(
                    "Smartcar user has {Count} connected vehicles; set Smartcar__VehicleId to choose one. Vehicle ids: {VehicleIds}",
                    vehicleIds.Count,
                    string.Join(", ", vehicleIds));
                return null;
            }

            _discoveredVehicleId = vehicleIds[0];
            _logger.LogInformation(
                "Using Smartcar vehicle {VehicleId} from the user's connection; set Smartcar__VehicleId to pin it.",
                _discoveredVehicleId);
            return _discoveredVehicleId;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Smartcar connections request timed out; treating fuel data as unavailable.");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
        {
            _logger.LogWarning(ex, "Smartcar connections request errored; treating fuel data as unavailable.");
            return null;
        }
    }

    // GET /vehicles/{vehicleId}/signals/{code} on behalf of the vehicle owner. Returns the signal
    // resource, or null when the request fails or the body can't be parsed.
    private async Task<SignalResource?> GetSignalAsync(
        string accessToken,
        string vehicleId,
        string signalCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{ApiBaseUrl}/vehicles/{Uri.EscapeDataString(vehicleId)}/signals/{signalCode}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("sc-user-id", _options.UserId);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                InvalidateTokenIfUnauthorized(response);

                // Covers rate limit (429), vehicle asleep, missing permission, and other API errors — degrade, don't throw.
                _logger.LogWarning(
                    "Smartcar {Signal} request failed with status {StatusCode}.", signalCode, (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var signal = JsonSerializer.Deserialize<SignalResponse>(body)?.Data;
            if (signal is null)
            {
                _logger.LogWarning("Smartcar {Signal} response could not be parsed.", signalCode);
                return null;
            }

            return signal;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Smartcar {Signal} request timed out; treating it as unavailable.", signalCode);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
        {
            _logger.LogWarning(ex, "Smartcar {Signal} request errored; treating it as unavailable.", signalCode);
            return null;
        }
    }

    // A 401 means the cached token is dead server-side — drop it so the next run requests a new one
    // instead of reusing it (and returning null) until the cache expires.
    private void InvalidateTokenIfUnauthorized(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _accessToken = null;
        }
    }

    // v3 reports fuel level as 0-100 with unit "percent", matching the FuelStatus contract directly.
    // A non-SUCCESS status (UNAVAILABLE/ERROR) is the "data unavailable" case: null percent.
    private double? MapPercent(SignalResource signal)
    {
        if (!IsSuccess(signal, FuelLevelSignal))
        {
            return null;
        }

        var body = signal.Attributes!.Body!;
        if (body.Unit is not null && !string.Equals(body.Unit, "percent", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Smartcar fuel level came back in unexpected unit {Unit}; treating it as unavailable.", body.Unit);
            return null;
        }

        return body.Value;
    }

    // Range carries its own unit (the docs' example is km); convert to the miles FuelStatus expects.
    private double? MapRangeMiles(SignalResource signal)
    {
        if (!IsSuccess(signal, RangeSignal))
        {
            return null;
        }

        var body = signal.Attributes!.Body!;
        switch (body.Unit?.ToLowerInvariant())
        {
            case "km":
            case "kilometers":
            case "kilometres":
                return body.Value * MilesPerKilometer;
            case "mi":
            case "miles":
                return body.Value;
            default:
                _logger.LogWarning("Smartcar range came back in unexpected unit {Unit}; omitting range.", body.Unit);
                return null;
        }
    }

    private bool IsSuccess(SignalResource signal, string signalCode)
    {
        var status = signal.Attributes?.Status?.Value;
        if (!string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Smartcar {Signal} status is {Status}; treating it as unavailable.", signalCode, status ?? "missing");
            return false;
        }

        if (signal.Attributes?.Body?.Value is null)
        {
            _logger.LogWarning("Smartcar {Signal} returned no value; treating it as unavailable.", signalCode);
            return false;
        }

        return true;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record ConnectionsResponse(
        [property: JsonPropertyName("data")] List<Connection>? Data);

    private sealed record Connection(
        [property: JsonPropertyName("relationships")] ConnectionRelationships? Relationships);

    private sealed record ConnectionRelationships(
        [property: JsonPropertyName("vehicle")] ResourceLink? Vehicle);

    private sealed record ResourceLink(
        [property: JsonPropertyName("data")] ResourceIdentifier? Data);

    private sealed record ResourceIdentifier(
        [property: JsonPropertyName("id")] string? Id);

    private sealed record SignalResponse(
        [property: JsonPropertyName("data")] SignalResource? Data);

    private sealed record SignalResource(
        [property: JsonPropertyName("attributes")] SignalAttributes? Attributes,
        [property: JsonPropertyName("meta")] SignalMeta? Meta);

    private sealed record SignalAttributes(
        [property: JsonPropertyName("status")] SignalStatus? Status,
        [property: JsonPropertyName("body")] SignalBody? Body);

    private sealed record SignalStatus(
        [property: JsonPropertyName("value")] string? Value);

    private sealed record SignalBody(
        [property: JsonPropertyName("value")] double? Value,
        [property: JsonPropertyName("unit")] string? Unit);

    private sealed record SignalMeta(
        [property: JsonPropertyName("retrievedAt")] DateTimeOffset? RetrievedAt);
}
