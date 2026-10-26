using System.Net;
using System.Text;
using System.Text.Json;
using FordFuelAlert.Functions.Models;
using FordFuelAlert.Functions.Services;
using FordFuelAlert.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FordFuelAlert.Tests;

public class FordConnectFuelServiceTests
{
    private const string TokenUrl = "https://api.vehicle.ford.com/dah2vb2cprod.onmicrosoft.com/oauth2/v2.0/token?p=B2C_1A_FCON_AUTHORIZE";
    private const string GarageUrl = "https://api.vehicle.ford.com/fcon-query/v1/garage";
    private const string TelemetryUrl = "https://api.vehicle.ford.com/fcon-query/v1/telemetry";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-26T12:00:00Z");
    private static readonly DateTimeOffset FuelUpdateTime = DateTimeOffset.Parse("2026-10-26T11:30:00Z");
    private static readonly DateTimeOffset RootUpdateTime = DateTimeOffset.Parse("2026-10-26T11:45:00Z");
    private static readonly DateTimeOffset AuthTime = DateTimeOffset.Parse("2026-10-26T09:00:00Z");

    private static string Base64Url(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string IdToken(DateTimeOffset authTime) =>
        $"{Base64Url("""{"alg":"RS256","typ":"JWT"}""")}.{Base64Url($$"""{"auth_time":{{authTime.ToUnixTimeSeconds()}},"sub":"owner"}""")}.signature";

    private static string TokenJson(
        string accessToken = "access-1",
        string? refreshToken = "refresh-rotated",
        object? expiresIn = null,
        bool includeIdToken = true)
    {
        var fields = new Dictionary<string, object?>
        {
            ["access_token"] = accessToken,
            ["token_type"] = "Bearer",
            ["expires_in"] = expiresIn ?? 3600,
        };
        if (refreshToken is not null)
        {
            fields["refresh_token"] = refreshToken;
        }

        if (includeIdToken)
        {
            fields["id_token"] = IdToken(AuthTime);
        }

        return JsonSerializer.Serialize(fields);
    }

    private static string TelemetryJson(
        double? fuelLevel = 40,
        double? fuelRange = 160.9344,
        string? fuelUpdateTime = "2026-10-26T11:30:00Z",
        bool includeFuelLevel = true)
    {
        var metrics = new Dictionary<string, object?>();
        if (includeFuelLevel)
        {
            var fuel = new Dictionary<string, object?> { ["value"] = fuelLevel };
            if (fuelUpdateTime is not null)
            {
                fuel["updateTime"] = fuelUpdateTime;
            }

            metrics["fuelLevel"] = fuel;
        }

        if (fuelRange is not null)
        {
            metrics["fuelRange"] = new { value = fuelRange, updateTime = "2026-10-26T11:30:00Z" };
        }

        metrics["odometer"] = new { value = 72100.5, updateTime = "2026-10-26T11:30:00Z" };
        metrics["ignitionStatus"] = new { value = "OFF", updateTime = "2026-10-26T11:30:00Z" };
        metrics["position"] = new { value = new { location = new { lat = 37.54, lon = -77.43 } }, updateTime = "2026-10-26T11:30:00Z" };

        return JsonSerializer.Serialize(new { updateTime = "2026-10-26T11:45:00Z", metrics });
    }

    private static string GarageJson(string vin = "1FT8W2BT5MED00001") =>
        JsonSerializer.Serialize(new { vin, vehicleType = "2021 F-250", make = "Ford", engineType = "ICE" });

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static bool IsToken(HttpRequestMessage r) => r.RequestUri!.ToString() == TokenUrl;
    private static bool IsGarage(HttpRequestMessage r) => r.RequestUri!.ToString() == GarageUrl;
    private static bool IsTelemetry(HttpRequestMessage r) => r.RequestUri!.ToString() == TelemetryUrl;

    private static FordConnectOptions DefaultOptions() => new()
    {
        ClientId = "client-1",
        ClientSecret = "secret-1",
        RefreshToken = "refresh-seed",
        Vin = "1FT8W2BT5MED00001",
    };

    private sealed class Harness
    {
        public required ScriptedHttpMessageHandler Handler { get; init; }
        public required FakeTableClient Table { get; init; }
        public required MutableTimeProvider Clock { get; init; }
        public required FordConnectFuelService Service { get; init; }
        public FordConnectTokenStore Store => new(Table, NullLogger<FordConnectTokenStore>.Instance);
    }

    private static Harness Build(
        ScriptedHttpMessageHandler handler,
        FordConnectOptions? options = null,
        FordConnectToken? storedToken = null,
        MutableTimeProvider? clock = null)
    {
        var table = new FakeTableClient();
        var timeProvider = clock ?? new MutableTimeProvider(Now);
        var store = new FordConnectTokenStore(table, NullLogger<FordConnectTokenStore>.Instance);
        if (storedToken is not null)
        {
            store.SaveAsync(storedToken).GetAwaiter().GetResult();
        }

        var service = new FordConnectFuelService(
            new SingleHandlerHttpClientFactory(handler),
            Options.Create(options ?? DefaultOptions()),
            store,
            timeProvider,
            NullLogger<FordConnectFuelService>.Instance);

        return new Harness { Handler = handler, Table = table, Clock = timeProvider, Service = service };
    }

    private static ScriptedHttpMessageHandler Handler(
        Func<HttpRequestMessage, HttpResponseMessage>? token = null,
        Func<HttpRequestMessage, HttpResponseMessage>? garage = null,
        Func<HttpRequestMessage, HttpResponseMessage>? telemetry = null) =>
        new(request =>
        {
            if (IsToken(request))
            {
                return token?.Invoke(request) ?? Json(HttpStatusCode.OK, TokenJson());
            }

            if (IsGarage(request))
            {
                return garage?.Invoke(request) ?? Json(HttpStatusCode.OK, GarageJson());
            }

            if (IsTelemetry(request))
            {
                return telemetry?.Invoke(request) ?? Json(HttpStatusCode.OK, TelemetryJson());
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

    [Fact]
    public async Task Happy_path_maps_percent_converts_km_range_and_uses_fuel_update_time()
    {
        var h = Build(Handler());

        var status = await h.Service.GetFuelStatusAsync();

        Assert.NotNull(status);
        Assert.Equal(40d, status!.PercentRemaining);
        Assert.Equal(100d, status.RangeMiles!.Value, precision: 3);
        Assert.Equal("1FT8W2BT5MED00001", status.VehicleId);
        Assert.Equal(FuelUpdateTime, status.RetrievedAtUtc);
    }

    [Theory]
    [InlineData(103.4, 100d)]
    [InlineData(-2, 0d)]
    [InlineData(25, 25d)]
    public async Task Clamps_fuel_level_into_0_to_100(double reported, double expected)
    {
        var h = Build(Handler(telemetry: _ => Json(HttpStatusCode.OK, TelemetryJson(fuelLevel: reported))));

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Equal(expected, status!.PercentRemaining);
    }

    [Fact]
    public async Task Refreshes_access_token_with_seed_refresh_token_when_store_is_empty()
    {
        var h = Build(Handler());

        await h.Service.GetFuelStatusAsync();

        var tokenRequest = h.Handler.Requests.Single(IsToken);
        Assert.Equal(HttpMethod.Post, tokenRequest.Method);
        var body = h.Handler.BodyFor(TokenUrl);
        Assert.NotNull(body);
        Assert.Contains("grant_type=refresh_token", body);
        Assert.Contains("client_id=client-1", body);
        Assert.Contains("client_secret=secret-1", body);
        Assert.Contains("refresh_token=refresh-seed", body);
    }

    [Fact]
    public async Task Prefers_stored_refresh_token_over_seed()
    {
        var h = Build(Handler(), storedToken: new FordConnectToken { RefreshToken = "refresh-stored" });

        await h.Service.GetFuelStatusAsync();

        Assert.Contains("refresh_token=refresh-stored", h.Handler.BodyFor(TokenUrl));
    }

    [Fact]
    public async Task Writes_rotated_refresh_token_and_auth_time_back_to_store()
    {
        var h = Build(Handler());

        await h.Service.GetFuelStatusAsync();

        var stored = await h.Store.GetAsync();
        Assert.NotNull(stored);
        Assert.Equal("refresh-rotated", stored.RefreshToken);
        Assert.Equal(AuthTime, stored.AuthTimeUtc);
        Assert.Equal(Now, stored.LastRefreshedUtc);
    }

    [Fact]
    public async Task Keeps_existing_refresh_token_and_auth_time_when_response_omits_them()
    {
        var earlierAuth = AuthTime.AddDays(-100);
        var h = Build(
            Handler(token: _ => Json(HttpStatusCode.OK, TokenJson(refreshToken: null, includeIdToken: false))),
            storedToken: new FordConnectToken { RefreshToken = "refresh-stored", AuthTimeUtc = earlierAuth });

        await h.Service.GetFuelStatusAsync();

        var stored = await h.Store.GetAsync();
        Assert.Equal("refresh-stored", stored!.RefreshToken);
        Assert.Equal(earlierAuth, stored.AuthTimeUtc);
        Assert.Equal(Now, stored.LastRefreshedUtc);
    }

    [Fact]
    public async Task Accepts_expires_in_as_a_string()
    {
        var h = Build(Handler(token: _ => Json(HttpStatusCode.OK, TokenJson(expiresIn: "3600"))));

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Equal(40d, status!.PercentRemaining);
    }

    [Fact]
    public async Task Throws_when_no_refresh_token_is_available_anywhere()
    {
        var handler = new ScriptedHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var options = DefaultOptions();
        options.RefreshToken = null;
        var h = Build(handler, options);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.GetFuelStatusAsync());

        Assert.Contains("FordConnect__RefreshToken", ex.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Throws_when_refresh_token_is_rejected_as_invalid_grant()
    {
        var body = """{"error":"invalid_grant","error_description":"AADB2C90080: The provided grant has expired."}""";
        var h = Build(Handler(token: _ => Json(HttpStatusCode.BadRequest, body)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.GetFuelStatusAsync());

        Assert.Contains("re-consent", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(h.Handler.Requests, IsTelemetry);
    }

    [Fact]
    public async Task Returns_null_when_token_endpoint_reports_a_transient_b2c_error()
    {
        var body = """{"error":"invalid_request","error_description":"AADB2C90085: The service has encountered an internal error."}""";
        var h = Build(Handler(token: _ => Json(HttpStatusCode.BadRequest, body)));

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Null(status);
        Assert.DoesNotContain(h.Handler.Requests, IsTelemetry);
    }

    private const string InvalidGrantJson = """{"error":"invalid_grant","error_description":"AADB2C90080: The provided grant has expired."}""";

    private static Func<HttpRequestMessage, HttpResponseMessage> RejectRefreshTokens(params string[] rejected) =>
        request =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return rejected.Any(token => body.Contains($"refresh_token={token}", StringComparison.Ordinal))
                ? Json(HttpStatusCode.BadRequest, InvalidGrantJson)
                : Json(HttpStatusCode.OK, TokenJson());
        };

    [Fact]
    public async Task Falls_back_to_seed_refresh_token_when_stored_token_is_rejected()
    {
        var h = Build(
            Handler(token: RejectRefreshTokens("refresh-stale")),
            storedToken: new FordConnectToken { RefreshToken = "refresh-stale", AuthTimeUtc = AuthTime.AddDays(-400) });

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Equal(40d, status!.PercentRemaining);
        Assert.Equal(2, h.Handler.Requests.Count(IsToken));
        Assert.Contains("refresh_token=refresh-stale", h.Handler.RequestBodies[0]);
        Assert.Contains("refresh_token=refresh-seed", h.Handler.RequestBodies[1]);
        var stored = await h.Store.GetAsync();
        Assert.Equal("refresh-rotated", stored!.RefreshToken);
        Assert.Equal(AuthTime, stored.AuthTimeUtc);
    }

    [Fact]
    public async Task Throws_when_stored_and_seed_refresh_tokens_are_both_rejected()
    {
        var h = Build(
            Handler(token: RejectRefreshTokens("refresh-stale", "refresh-seed")),
            storedToken: new FordConnectToken { RefreshToken = "refresh-stale" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.GetFuelStatusAsync());

        Assert.Contains("re-consent", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, h.Handler.Requests.Count(IsToken));
        Assert.DoesNotContain(h.Handler.Requests, IsTelemetry);
    }

    [Fact]
    public async Task Does_not_retry_when_seed_equals_the_rejected_stored_token()
    {
        var h = Build(
            Handler(token: RejectRefreshTokens("refresh-seed")),
            storedToken: new FordConnectToken { RefreshToken = "refresh-seed" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.GetFuelStatusAsync());

        Assert.Single(h.Handler.Requests, IsToken);
    }

    [Fact]
    public async Task Returns_null_when_token_endpoint_fails_transiently()
    {
        var h = Build(Handler(token: _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Null(status);
        Assert.DoesNotContain(h.Handler.Requests, IsTelemetry);
    }

    [Fact]
    public async Task Returns_null_on_token_timeout()
    {
        var h = Build(Handler(token: _ => throw new TaskCanceledException("timeout")));

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task Caches_access_token_until_it_nears_expiry()
    {
        var clock = new MutableTimeProvider(Now);
        var h = Build(Handler(), clock: clock);

        await h.Service.GetFuelStatusAsync();
        clock.Advance(TimeSpan.FromMinutes(30));
        await h.Service.GetFuelStatusAsync();
        clock.Advance(TimeSpan.FromMinutes(30));
        await h.Service.GetFuelStatusAsync();

        Assert.Equal(2, h.Handler.Requests.Count(IsToken));
        Assert.Equal(3, h.Handler.Requests.Count(IsTelemetry));
    }

    [Fact]
    public async Task Invalidates_cached_token_on_unauthorized_telemetry_response()
    {
        var h = Build(Handler(telemetry: _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        await h.Service.GetFuelStatusAsync();
        await h.Service.GetFuelStatusAsync();

        Assert.Equal(2, h.Handler.Requests.Count(IsToken));
    }

    [Fact]
    public async Task Sends_bearer_token_on_telemetry_request()
    {
        var h = Build(Handler());

        await h.Service.GetFuelStatusAsync();

        var request = h.Handler.Requests.Single(IsTelemetry);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("access-1", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Skips_garage_lookup_when_vin_configured()
    {
        var h = Build(Handler());

        await h.Service.GetFuelStatusAsync();

        Assert.DoesNotContain(h.Handler.Requests, IsGarage);
    }

    [Fact]
    public async Task Discovers_vin_from_garage_once_when_not_configured()
    {
        var options = DefaultOptions();
        options.Vin = null;
        var h = Build(Handler(garage: _ => Json(HttpStatusCode.OK, GarageJson("1FMJU1JT5MEA00002"))), options);

        var first = await h.Service.GetFuelStatusAsync();
        var second = await h.Service.GetFuelStatusAsync();

        Assert.Equal("1FMJU1JT5MEA00002", first!.VehicleId);
        Assert.Equal("1FMJU1JT5MEA00002", second!.VehicleId);
        var garageRequest = h.Handler.Requests.Single(IsGarage);
        Assert.Equal("access-1", garageRequest.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task Keeps_fuel_reading_with_null_vehicle_id_when_garage_fails()
    {
        var options = DefaultOptions();
        options.Vin = null;
        var h = Build(Handler(garage: _ => new HttpResponseMessage((HttpStatusCode)429)), options);

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Equal(40d, status!.PercentRemaining);
        Assert.Null(status.VehicleId);
    }

    [Fact]
    public async Task Returns_null_when_fuel_level_metric_is_missing()
    {
        var h = Build(Handler(telemetry: _ => Json(HttpStatusCode.OK, TelemetryJson(includeFuelLevel: false))));

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task Returns_null_when_fuel_level_value_is_missing()
    {
        var h = Build(Handler(telemetry: _ => Json(HttpStatusCode.OK, TelemetryJson(fuelLevel: null))));

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task Keeps_percent_when_range_metric_is_missing()
    {
        var h = Build(Handler(telemetry: _ => Json(HttpStatusCode.OK, TelemetryJson(fuelRange: null))));

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Equal(40d, status!.PercentRemaining);
        Assert.Null(status.RangeMiles);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task Returns_null_when_telemetry_request_fails(HttpStatusCode code)
    {
        var h = Build(Handler(telemetry: _ => new HttpResponseMessage(code)));

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task Returns_null_on_malformed_telemetry_json()
    {
        var h = Build(Handler(telemetry: _ => Json(HttpStatusCode.OK, "not json")));

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task Returns_null_on_telemetry_transport_exception()
    {
        var h = Build(Handler(telemetry: _ => throw new HttpRequestException("connection reset")));

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task Falls_back_to_root_update_time_when_fuel_metric_has_none()
    {
        var h = Build(Handler(telemetry: _ => Json(HttpStatusCode.OK, TelemetryJson(fuelUpdateTime: null))));

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Equal(RootUpdateTime, status!.RetrievedAtUtc);
    }

    [Fact]
    public async Task Falls_back_to_clock_when_no_update_time_at_all()
    {
        var h = Build(Handler(telemetry: _ => Json(
            HttpStatusCode.OK,
            """{"metrics":{"fuelLevel":{"value":30}}}""")));

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Equal(30d, status!.PercentRemaining);
        Assert.Equal(Now, status.RetrievedAtUtc);
    }

    [Fact]
    public async Task Still_returns_stale_reading_older_than_max_data_age()
    {
        var options = DefaultOptions();
        options.MaxDataAgeHours = 1;
        var h = Build(Handler(telemetry: _ => Json(
            HttpStatusCode.OK,
            TelemetryJson(fuelLevel: 22, fuelUpdateTime: "2026-10-20T08:00:00Z"))), options);

        var status = await h.Service.GetFuelStatusAsync();

        Assert.Equal(22d, status!.PercentRemaining);
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T08:00:00Z"), status.RetrievedAtUtc);
    }

    [Fact]
    public async Task Propagates_genuine_caller_cancellation()
    {
        var handler = new ScriptedHttpMessageHandler(_ => throw new OperationCanceledException());
        var h = Build(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Service.GetFuelStatusAsync(cts.Token));
    }

    [Fact]
    public async Task Low_reading_drives_the_alert_pipeline_end_to_end()
    {
        var h = Build(Handler(telemetry: _ => Json(HttpStatusCode.OK, TelemetryJson(fuelLevel: 18, fuelRange: 64.37376))));
        var email = new RecordingEmailService();
        var state = new InMemoryAlertStateService();
        var alerts = new FuelAlertService(
            h.Service,
            email,
            state,
            Options.Create(new FuelAlertOptions()),
            new MutableTimeProvider(Now),
            NullLogger<FuelAlertService>.Instance);

        var first = await alerts.EvaluateAsync();
        var second = await alerts.EvaluateAsync();

        Assert.True(first.AlertSent);
        Assert.False(second.AlertSent);
        var message = Assert.Single(email.Sent);
        Assert.Contains("18%", message.Body);
        Assert.Equal(18d, state.Current!.LastKnownPercent);
        Assert.Equal(40d, state.Current.LastKnownRangeMiles!.Value, precision: 3);
    }
}
