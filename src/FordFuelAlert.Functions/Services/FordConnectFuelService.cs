using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FordFuelAlert.Functions.Services;

public sealed class FordConnectFuelService : IFuelService
{
    private const string TokenUrl = "https://api.vehicle.ford.com/dah2vb2cprod.onmicrosoft.com/oauth2/v2.0/token?p=B2C_1A_FCON_AUTHORIZE";
    private const string QueryBaseUrl = "https://api.vehicle.ford.com/fcon-query/v1";

    private const string FuelLevelMetric = "fuelLevel";
    private const string FuelRangeMetric = "fuelRange";

    private const double MilesPerKilometer = 0.621371;
    private const int ExpiryBufferSeconds = 60;

    private static readonly TimeSpan ConsentLifetime = TimeSpan.FromDays(365);
    private static readonly TimeSpan ConsentWarningWindow = TimeSpan.FromDays(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FordConnectOptions _options;
    private readonly FordConnectTokenStore _tokenStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FordConnectFuelService> _logger;

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresUtc;
    private string? _discoveredVin;

    public FordConnectFuelService(
        IHttpClientFactory httpClientFactory,
        IOptions<FordConnectOptions> options,
        FordConnectTokenStore tokenStore,
        TimeProvider timeProvider,
        ILogger<FordConnectFuelService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _tokenStore = tokenStore;
        _timeProvider = timeProvider;
        _logger = logger;

        _logger.LogInformation(
            "FordConnect provider configured (client id: {HasClientId}, seed refresh token: {HasSeed}, vin: {VinMode}).",
            !string.IsNullOrWhiteSpace(_options.ClientId),
            !string.IsNullOrWhiteSpace(_options.RefreshToken),
            string.IsNullOrWhiteSpace(_options.Vin) ? "discover from garage" : "pinned");
    }

    public async Task<FuelStatus?> GetFuelStatusAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        if (accessToken is null)
        {
            return null;
        }

        var vin = await GetVinAsync(accessToken, cancellationToken);

        var telemetry = await GetTelemetryAsync(accessToken, cancellationToken);
        if (telemetry is null)
        {
            return null;
        }

        var fuelLevel = ReadMetric(telemetry, FuelLevelMetric);
        if (fuelLevel?.Value is null)
        {
            _logger.LogWarning("FordConnect telemetry has no numeric {Metric} value; treating fuel data as unavailable.", FuelLevelMetric);
            return null;
        }

        var retrievedAt = fuelLevel.UpdateTime ?? telemetry.UpdateTime ?? _timeProvider.GetUtcNow();
        WarnIfStale(retrievedAt);

        var range = ReadMetric(telemetry, FuelRangeMetric)?.Value;

        return new FuelStatus
        {
            PercentRemaining = Math.Clamp(fuelLevel.Value.Value, 0, 100),
            RangeMiles = range is null ? null : range.Value * MilesPerKilometer,
            RetrievedAtUtc = retrievedAt,
            VehicleId = vin,
        };
    }

    private async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        if (_accessToken is not null && now < _accessTokenExpiresUtc)
        {
            return _accessToken;
        }

        var stored = await _tokenStore.GetAsync(cancellationToken);
        var refreshToken = stored?.RefreshToken
            ?? (string.IsNullOrWhiteSpace(_options.RefreshToken) ? null : _options.RefreshToken);
        if (refreshToken is null)
        {
            throw new InvalidOperationException(
                "No FordConnect refresh token is available. Complete the one-time FordConnect consent and set "
                + "FordConnect__RefreshToken to seed the token store.");
        }

        WarnIfConsentExpiring(stored?.AuthTimeUtc, now);

        HttpResponseMessage response;
        string body;
        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("client_id", _options.ClientId ?? string.Empty),
                new KeyValuePair<string, string>("client_secret", _options.ClientSecret ?? string.Empty),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
            });

            response = await client.SendAsync(request, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("FordConnect token refresh timed out; treating fuel data as unavailable.");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            _logger.LogWarning(ex, "FordConnect token refresh errored; treating fuel data as unavailable.");
            return null;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = ReadOAuthError(body);
                if (string.Equals(error, "invalid_grant", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "FordConnect rejected the refresh token (invalid_grant). Ford requires the owner to re-consent "
                        + "every 365 days; redo the FordConnect consent and update FordConnect__RefreshToken.");
                }

                _logger.LogWarning(
                    "FordConnect token refresh failed with status {StatusCode} (error: {Error}); treating fuel data as unavailable.",
                    (int)response.StatusCode,
                    error ?? "none");
                return null;
            }
        }

        TokenResponse? token;
        try
        {
            token = JsonSerializer.Deserialize<TokenResponse>(body);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "FordConnect token response could not be parsed; treating fuel data as unavailable.");
            return null;
        }

        if (token?.AccessToken is null)
        {
            _logger.LogWarning("FordConnect token response contained no access token; treating fuel data as unavailable.");
            return null;
        }

        _accessToken = token.AccessToken;
        _accessTokenExpiresUtc = now.AddSeconds(Math.Max(0, ParseExpiresIn(token.ExpiresIn) - ExpiryBufferSeconds));

        await _tokenStore.SaveAsync(
            new FordConnectToken
            {
                RefreshToken = token.RefreshToken ?? refreshToken,
                AuthTimeUtc = ParseAuthTime(token.IdToken) ?? stored?.AuthTimeUtc,
                LastRefreshedUtc = now,
            },
            cancellationToken);

        return _accessToken;
    }

    private async Task<string?> GetVinAsync(string accessToken, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.Vin))
        {
            return _options.Vin;
        }

        if (_discoveredVin is not null)
        {
            return _discoveredVin;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{QueryBaseUrl}/garage");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                InvalidateTokenIfUnauthorized(response);
                _logger.LogWarning("FordConnect garage request failed with status {StatusCode}; vehicle id will be retried next run.", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var vin = JsonSerializer.Deserialize<GarageResponse>(body)?.Vin;
            if (string.IsNullOrWhiteSpace(vin))
            {
                _logger.LogWarning("FordConnect garage response contained no VIN; set FordConnect__Vin to pin it.");
                return null;
            }

            _discoveredVin = vin;
            _logger.LogInformation("Using VIN {Vin} from the FordConnect garage; set FordConnect__Vin to pin it.", vin);
            return _discoveredVin;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("FordConnect garage request timed out; vehicle id will be retried next run.");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
        {
            _logger.LogWarning(ex, "FordConnect garage request errored; vehicle id will be retried next run.");
            return null;
        }
    }

    private async Task<TelemetryResponse?> GetTelemetryAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{QueryBaseUrl}/telemetry");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                InvalidateTokenIfUnauthorized(response);
                _logger.LogWarning("FordConnect telemetry request failed with status {StatusCode}; treating fuel data as unavailable.", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var telemetry = JsonSerializer.Deserialize<TelemetryResponse>(body);
            if (telemetry is null)
            {
                _logger.LogWarning("FordConnect telemetry response was empty; treating fuel data as unavailable.");
                return null;
            }

            return telemetry;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("FordConnect telemetry request timed out; treating fuel data as unavailable.");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
        {
            _logger.LogWarning(ex, "FordConnect telemetry request errored; treating fuel data as unavailable.");
            return null;
        }
    }

    private void InvalidateTokenIfUnauthorized(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _accessToken = null;
        }
    }

    private void WarnIfStale(DateTimeOffset retrievedAt)
    {
        var age = _timeProvider.GetUtcNow() - retrievedAt;
        if (age > TimeSpan.FromHours(_options.MaxDataAgeHours))
        {
            _logger.LogWarning(
                "FordConnect fuel reading is {AgeHours:F1} hours old (last update {UpdateTime:u}); the vehicle may not be reporting.",
                age.TotalHours,
                retrievedAt);
        }
    }

    private void WarnIfConsentExpiring(DateTimeOffset? authTime, DateTimeOffset now)
    {
        if (authTime is null)
        {
            return;
        }

        var deadline = authTime.Value + ConsentLifetime;
        if (now >= deadline - ConsentWarningWindow)
        {
            _logger.LogWarning(
                "FordConnect consent expires {Deadline:u}; the owner must re-consent before then or fuel reads will stop.",
                deadline);
        }
    }

    private static Metric? ReadMetric(TelemetryResponse telemetry, string key)
    {
        if (telemetry.Metrics is null
            || !telemetry.Metrics.TryGetValue(key, out var element)
            || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        double? value = element.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Number
            ? valueElement.GetDouble()
            : null;

        return new Metric(value, ParseTimestamp(element.TryGetProperty("updateTime", out var timeElement) ? timeElement : default));
    }

    private static DateTimeOffset? ParseTimestamp(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            element.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static int ParseExpiresIn(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number when element.TryGetInt32(out var number) => number,
        JsonValueKind.String when int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => 0,
    };

    private static string? ReadOAuthError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
                ? error.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateTimeOffset? ParseAuthTime(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        var segments = idToken.Split('.');
        if (segments.Length < 2)
        {
            return null;
        }

        try
        {
            var payload = segments[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return document.RootElement.TryGetProperty("auth_time", out var authTime) && authTime.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("id_token")] string? IdToken,
        [property: JsonPropertyName("expires_in")] JsonElement ExpiresIn);

    private sealed record GarageResponse(
        [property: JsonPropertyName("vin")] string? Vin);

    private sealed record TelemetryResponse(
        [property: JsonPropertyName("updateTime")] string? UpdateTimeRaw,
        [property: JsonPropertyName("metrics")] Dictionary<string, JsonElement>? Metrics)
    {
        public DateTimeOffset? UpdateTime => string.IsNullOrWhiteSpace(UpdateTimeRaw)
            ? null
            : DateTimeOffset.TryParse(UpdateTimeRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
                ? parsed
                : null;
    }

    private sealed record Metric(double? Value, DateTimeOffset? UpdateTime);
}
