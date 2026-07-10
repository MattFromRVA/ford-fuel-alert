using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FordFuelAlert.Functions.Services;

// Real fuel provider backed by the Smartcar API (https://smartcar.com/docs), selected when
// FuelAlert__UseFakeFuelService=false. Two calls per refresh: exchange the rotating refresh token
// for an access token, then read the fuel endpoint. Every failure path degrades to null (logged
// warning) so a bad reading never crashes the timer run — the alert logic treats null as "no data".
public sealed class SmartcarFuelService : IFuelService
{
    private const string TokenUrl = "https://auth.smartcar.com/oauth/token";
    private const string ApiBaseUrl = "https://api.smartcar.com/v2.0";

    // Refresh a little before the token actually expires so an in-flight call can't race the boundary.
    private const int ExpiryBufferSeconds = 60;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISmartcarTokenStore _tokenStore;
    private readonly SmartcarOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SmartcarFuelService> _logger;

    // Cached access token for the process lifetime. The function host runs timer executions
    // sequentially on this singleton, so no locking is needed (same assumption as TableAlertStateService).
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresUtc;

    // Most recent refresh token held in memory. Since Smartcar rotates the token on every exchange,
    // this is the freshest value — preferred over the store read, so a failed persist can't make a
    // later run present a stale (already-invalidated) token and strand the integration.
    private string? _refreshToken;

    public SmartcarFuelService(
        IHttpClientFactory httpClientFactory,
        ISmartcarTokenStore tokenStore,
        IOptions<SmartcarOptions> options,
        TimeProvider timeProvider,
        ILogger<SmartcarFuelService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenStore = tokenStore;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<FuelStatus?> GetFuelStatusAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        if (accessToken is null)
        {
            return null;
        }

        return await GetFuelAsync(accessToken, cancellationToken);
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
            // Prefer the in-memory rotated token, then the persisted one, then the configured bootstrap
            // token on first run. The store read is inside the try so a transient Table Storage failure
            // degrades to null too.
            var refreshToken = _refreshToken;
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                refreshToken = await _tokenStore.GetRefreshTokenAsync(cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                refreshToken = _options.RefreshToken;
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                _logger.LogWarning(
                    "No Smartcar refresh token available (store empty and Smartcar__RefreshToken unset); cannot fetch fuel data.");
                return null;
            }

            var client = _httpClientFactory.CreateClient();

            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
            });

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Smartcar token exchange failed with status {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var token = JsonSerializer.Deserialize<TokenResponse>(body);
            if (token?.AccessToken is null)
            {
                _logger.LogWarning("Smartcar token exchange returned no access token.");
                return null;
            }

            // Smartcar rotates the refresh token on every exchange. Retain the new one in memory
            // first (so it survives even if the store write fails), then try to persist it. A failed
            // persist is logged but does NOT abort the run — we still hold a valid access token, and
            // the retained token is retried on the next exchange.
            if (!string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                _refreshToken = token.RefreshToken;
                try
                {
                    await _tokenStore.SaveRefreshTokenAsync(token.RefreshToken, cancellationToken);
                }
                catch (RequestFailedException ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to persist the rotated Smartcar refresh token; retained in memory and will retry next run. "
                        + "If the host recycles before it persists, re-authorization may be required.");
                }
            }

            _accessToken = token.AccessToken;
            _accessTokenExpiresUtc = now.AddSeconds(Math.Max(0, token.ExpiresIn - ExpiryBufferSeconds));
            return _accessToken;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient surfaces a request timeout as (Task)OperationCanceledException even when the
            // caller didn't cancel — treat that as unavailable data, but let real cancellation propagate.
            _logger.LogWarning("Smartcar token exchange timed out; treating fuel data as unavailable.");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or RequestFailedException)
        {
            // Includes token-store (RequestFailedException) and mid-stream body-read (IOException) failures.
            _logger.LogWarning(ex, "Smartcar token acquisition errored; treating fuel data as unavailable.");
            return null;
        }
    }

    private async Task<FuelStatus?> GetFuelAsync(string accessToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.VehicleId))
        {
            _logger.LogWarning("Smartcar__VehicleId is not configured; cannot fetch fuel data.");
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/vehicles/{_options.VehicleId}/fuel");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("SC-Unit-System", "imperial"); // return range in miles

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // A 401 means the cached access token is dead server-side — drop it so the next run
                // re-exchanges instead of reusing it (and returning null) until the cache expires.
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _accessToken = null;
                }

                // Covers rate limit (429), vehicle asleep, and other API errors — degrade, don't throw.
                _logger.LogWarning("Smartcar fuel request failed with status {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var fuel = JsonSerializer.Deserialize<FuelResponse>(body);
            if (fuel is null)
            {
                _logger.LogWarning("Smartcar fuel response could not be parsed.");
                return null;
            }

            return new FuelStatus
            {
                // Smartcar returns percentRemaining as a 0-1 decimal; scale to the 0-100 FuelStatus contract.
                PercentRemaining = fuel.PercentRemaining.HasValue ? fuel.PercentRemaining.Value * 100 : null,
                RangeMiles = fuel.Range,
                RetrievedAtUtc = _timeProvider.GetUtcNow(),
                VehicleId = _options.VehicleId,
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Smartcar fuel request timed out; treating fuel data as unavailable.");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
        {
            _logger.LogWarning(ex, "Smartcar fuel request errored; treating fuel data as unavailable.");
            return null;
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);

    // Smartcar's fuel response also carries "amountRemaining" (liters/gallons); FuelStatus has no field
    // for it, so it is intentionally not deserialized (System.Text.Json ignores the extra property).
    private sealed record FuelResponse(
        [property: JsonPropertyName("percentRemaining")] double? PercentRemaining,
        [property: JsonPropertyName("range")] double? Range);
}
