using System.Net;
using System.Text;
using System.Text.Json;
using FordFuelAlert.Functions.Models;
using FordFuelAlert.Functions.Services;
using FordFuelAlert.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FordFuelAlert.Tests;

// Response shapes follow Smartcar's v3 docs: Request Access Token (client credentials), Get Signal
// (data.attributes.status/body + data.meta.retrievedAt), and List Connections
// (data[].relationships.vehicle.data.id).
public class SmartcarFuelServiceTests
{
    private const string TokenUrl = "https://iam.smartcar.com/oauth2/token";
    private const string ApiBase = "https://vehicle.api.smartcar.com/v3";
    private const string FuelLevelPath = "/vehicles/veh-1/signals/internalcombustionengine-fuellevel";
    private const string RangePath = "/vehicles/veh-1/signals/internalcombustionengine-range";
    private const string ConnectionsPath = "/connections";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-22T12:00:00Z");

    private static string TokenJson(string accessToken = "access-1", int expiresIn = 3600) =>
        $$"""{"access_token":"{{accessToken}}","token_type":"Bearer","expires_in":{{expiresIn}}}""";

    private static string SignalJson(
        string code,
        double? value,
        string? unit,
        string status = "SUCCESS",
        string retrievedAt = "2026-10-22T11:58:00Z") =>
        JsonSerializer.Serialize(new
        {
            data = new
            {
                id = code,
                type = "signal",
                attributes = new
                {
                    code,
                    name = "n",
                    group = "InternalCombustionEngine",
                    status = new { value = status },
                    body = new { value, unit },
                },
                meta = new { retrievedAt, oemUpdatedAt = retrievedAt },
            },
            included = new { vehicle = new { id = "veh-1", type = "vehicle", attributes = new { make = "FORD", mode = "live" } } },
        });

    private static string ConnectionsJson(params string[] vehicleIds) =>
        JsonSerializer.Serialize(new
        {
            data = vehicleIds.Select(id => new
            {
                id = $"conn-{id}",
                type = "connection",
                attributes = new { permissions = new[] { "read_fuel" }, user = new { id = "user-1" }, vehicle = new { make = "FORD" } },
                relationships = new
                {
                    vehicle = new { data = new { id, type = "vehicle" } },
                    user = new { data = new { id = "user-1", type = "user" } },
                },
            }),
            meta = new { pageNumber = 1, pageSize = 10, totalCount = vehicleIds.Length },
        });

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static bool Is(HttpRequestMessage request, string path) =>
        request.RequestUri!.GetLeftPart(UriPartial.Path).EndsWith(path, StringComparison.Ordinal);

    private static SmartcarOptions DefaultOptions() => new()
    {
        ClientId = "client-1",
        ClientSecret = "secret-1",
        UserId = "user-1",
        VehicleId = "veh-1",
    };

    private static SmartcarFuelService Build(
        ScriptedHttpMessageHandler handler,
        SmartcarOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        return new SmartcarFuelService(
            new SingleHandlerHttpClientFactory(handler),
            Options.Create(options ?? DefaultOptions()),
            timeProvider ?? new MutableTimeProvider(Now),
            NullLogger<SmartcarFuelService>.Instance);
    }

    // Routes the Smartcar calls: token (iam host), connections, and the two signals (vehicle API host).
    // Any override returns non-null to replace the default response for that call.
    private static ScriptedHttpMessageHandler Handler(
        Func<HttpRequestMessage, HttpResponseMessage>? fuelLevel = null,
        Func<HttpRequestMessage, HttpResponseMessage>? range = null,
        Func<HttpRequestMessage, HttpResponseMessage>? token = null,
        Func<HttpRequestMessage, HttpResponseMessage>? connections = null) =>
        new(request =>
        {
            if (request.RequestUri!.ToString() == TokenUrl)
            {
                return token?.Invoke(request) ?? Json(HttpStatusCode.OK, TokenJson());
            }

            if (Is(request, ConnectionsPath))
            {
                return connections?.Invoke(request) ?? Json(HttpStatusCode.OK, ConnectionsJson("veh-1"));
            }

            if (Is(request, FuelLevelPath))
            {
                return fuelLevel?.Invoke(request)
                    ?? Json(HttpStatusCode.OK, SignalJson("internalcombustionengine-fuellevel", 40, "percent"));
            }

            if (Is(request, RangePath))
            {
                return range?.Invoke(request)
                    ?? Json(HttpStatusCode.OK, SignalJson("internalcombustionengine-range", 160.9344, "km"));
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

    [Fact]
    public async Task Happy_path_maps_percent_as_is_and_converts_km_range_to_miles()
    {
        var status = await Build(Handler()).GetFuelStatusAsync();

        Assert.NotNull(status);
        Assert.Equal(40d, status!.PercentRemaining);            // v3 is already 0-100; no scaling
        Assert.Equal(100d, status.RangeMiles!.Value, precision: 3); // 160.9344 km
        Assert.Equal("veh-1", status.VehicleId);
        Assert.Equal(DateTimeOffset.Parse("2026-10-22T11:58:00Z"), status.RetrievedAtUtc);
    }

    [Fact]
    public async Task Passes_range_in_miles_through_unchanged()
    {
        var handler = Handler(range: _ =>
            Json(HttpStatusCode.OK, SignalJson("internalcombustionengine-range", 150, "miles")));

        var status = await Build(handler).GetFuelStatusAsync();

        Assert.Equal(150d, status!.RangeMiles);
    }

    [Fact]
    public async Task Requests_app_token_with_client_credentials_in_form_body()
    {
        var handler = Handler();

        await Build(handler).GetFuelStatusAsync();

        var tokenRequest = handler.Requests.Single(r => r.RequestUri!.ToString() == TokenUrl);
        Assert.Equal(HttpMethod.Post, tokenRequest.Method);
        Assert.Null(tokenRequest.Headers.Authorization); // credentials go in the body, not Basic auth

        var body = handler.BodyFor(TokenUrl);
        Assert.NotNull(body);
        Assert.Contains("grant_type=client_credentials", body);
        Assert.Contains("client_id=client-1", body);
        Assert.Contains("client_secret=secret-1", body);
    }

    [Fact]
    public async Task Sends_bearer_token_and_sc_user_id_on_signal_requests()
    {
        var handler = Handler();

        await Build(handler).GetFuelStatusAsync();

        foreach (var path in new[] { FuelLevelPath, RangePath })
        {
            var request = handler.Requests.Single(r => Is(r, path));
            Assert.Equal(ApiBase + path, request.RequestUri!.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("access-1", request.Headers.Authorization.Parameter);
            Assert.Equal("user-1", request.Headers.GetValues("sc-user-id").Single());
        }
    }

    [Fact]
    public async Task Returns_null_and_makes_no_http_calls_when_user_id_missing()
    {
        var handler = new ScriptedHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var options = DefaultOptions();
        options.UserId = "";

        var status = await Build(handler, options).GetFuelStatusAsync();

        Assert.Null(status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Returns_null_and_makes_no_http_calls_when_client_credentials_missing()
    {
        var handler = new ScriptedHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var options = DefaultOptions();
        options.ClientSecret = null;

        var status = await Build(handler, options).GetFuelStatusAsync();

        Assert.Null(status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Returns_null_and_makes_no_signal_call_when_token_request_fails()
    {
        var handler = Handler(token: _ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        var status = await Build(handler).GetFuelStatusAsync();

        Assert.Null(status);
        Assert.DoesNotContain(handler.Requests, r => Is(r, FuelLevelPath));
    }

    [Fact]
    public async Task Caches_app_token_until_it_nears_expiry()
    {
        var handler = Handler();
        var clock = new MutableTimeProvider(Now);
        var service = Build(handler, timeProvider: clock);

        await service.GetFuelStatusAsync();
        clock.Advance(TimeSpan.FromMinutes(30));
        await service.GetFuelStatusAsync(); // still inside the 1-hour token lifetime
        clock.Advance(TimeSpan.FromMinutes(30));
        await service.GetFuelStatusAsync(); // past expiry minus the 60s buffer

        Assert.Equal(2, handler.Requests.Count(r => r.RequestUri!.ToString() == TokenUrl));
        Assert.Equal(3, handler.Requests.Count(r => Is(r, FuelLevelPath)));
    }

    [Fact]
    public async Task Invalidates_cached_token_on_unauthorized_signal_response()
    {
        // A 401 means the cached token is dead server-side; the next run must request a new one
        // rather than reuse it for the rest of the hour.
        var handler = Handler(fuelLevel: _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var service = Build(handler);

        await service.GetFuelStatusAsync();
        await service.GetFuelStatusAsync();

        Assert.Equal(2, handler.Requests.Count(r => r.RequestUri!.ToString() == TokenUrl));
    }

    [Fact]
    public async Task Returns_null_when_fuel_level_request_returns_error_status()
    {
        var handler = Handler(fuelLevel: _ => new HttpResponseMessage((HttpStatusCode)429));

        var status = await Build(handler).GetFuelStatusAsync();

        Assert.Null(status);
        Assert.DoesNotContain(handler.Requests, r => Is(r, RangePath));
    }

    [Theory]
    [InlineData("UNAVAILABLE")]
    [InlineData("ERROR")]
    public async Task Returns_null_percent_when_fuel_level_status_is_not_success(string signalStatus)
    {
        // Data-unavailable (manufacturer gap, vehicle asleep): a FuelStatus with null percent, which
        // the alert logic already treats as "no decision".
        var handler = Handler(fuelLevel: _ =>
            Json(HttpStatusCode.OK, SignalJson("internalcombustionengine-fuellevel", null, null, signalStatus)));

        var status = await Build(handler).GetFuelStatusAsync();

        Assert.NotNull(status);
        Assert.Null(status!.PercentRemaining);
    }

    [Fact]
    public async Task Returns_null_percent_when_fuel_level_unit_is_not_percent()
    {
        var handler = Handler(fuelLevel: _ =>
            Json(HttpStatusCode.OK, SignalJson("internalcombustionengine-fuellevel", 12, "gallons")));

        var status = await Build(handler).GetFuelStatusAsync();

        Assert.NotNull(status);
        Assert.Null(status!.PercentRemaining);
    }

    [Fact]
    public async Task Keeps_percent_when_range_request_fails()
    {
        var handler = Handler(range: _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var status = await Build(handler).GetFuelStatusAsync();

        Assert.Equal(40d, status!.PercentRemaining);
        Assert.Null(status.RangeMiles);
    }

    [Fact]
    public async Task Omits_range_in_unknown_unit()
    {
        var handler = Handler(range: _ =>
            Json(HttpStatusCode.OK, SignalJson("internalcombustionengine-range", 300, "furlongs")));

        var status = await Build(handler).GetFuelStatusAsync();

        Assert.Equal(40d, status!.PercentRemaining);
        Assert.Null(status.RangeMiles);
    }

    [Fact]
    public async Task Falls_back_to_clock_when_signal_has_no_retrieved_at()
    {
        var handler = Handler(fuelLevel: _ => Json(
            HttpStatusCode.OK,
            """{"data":{"attributes":{"status":{"value":"SUCCESS"},"body":{"value":30,"unit":"percent"}}}}"""));

        var status = await Build(handler).GetFuelStatusAsync();

        Assert.Equal(30d, status!.PercentRemaining);
        Assert.Equal(Now, status.RetrievedAtUtc);
    }

    [Fact]
    public async Task Returns_null_on_transport_exception()
    {
        var handler = Handler(fuelLevel: _ => throw new HttpRequestException("connection reset"));

        var status = await Build(handler).GetFuelStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task Returns_null_on_malformed_signal_json()
    {
        var handler = Handler(fuelLevel: _ => Json(HttpStatusCode.OK, "this is not json"));

        var status = await Build(handler).GetFuelStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task Returns_null_on_token_timeout()
    {
        // HttpClient surfaces a timeout as TaskCanceledException even when the caller didn't cancel;
        // that must degrade to null, not crash the timer run.
        var handler = Handler(token: _ => throw new TaskCanceledException("timeout"));

        var status = await Build(handler).GetFuelStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task Returns_null_on_fuel_level_timeout()
    {
        var handler = Handler(fuelLevel: _ => throw new TaskCanceledException("timeout"));

        var status = await Build(handler).GetFuelStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task Propagates_genuine_caller_cancellation()
    {
        // A real caller cancellation must NOT be swallowed as "data unavailable".
        var handler = new ScriptedHttpMessageHandler(_ => throw new OperationCanceledException());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Build(handler).GetFuelStatusAsync(cts.Token));
    }

    [Fact]
    public async Task Discovers_vehicle_id_from_single_connection_when_not_configured()
    {
        var handler = Handler();
        var options = DefaultOptions();
        options.VehicleId = null;
        var service = Build(handler, options);

        var first = await service.GetFuelStatusAsync();
        var second = await service.GetFuelStatusAsync();

        Assert.Equal("veh-1", first!.VehicleId);
        Assert.Equal(40d, second!.PercentRemaining);

        // Looked up once, then reused.
        var connectionsRequest = handler.Requests.Single(r => Is(r, ConnectionsPath));
        Assert.Equal("user-1", Uri.UnescapeDataString(connectionsRequest.RequestUri!.Query).Split("filter[userId]=")[1]);
        Assert.Equal("access-1", connectionsRequest.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task Skips_connection_lookup_when_vehicle_id_configured()
    {
        var handler = Handler();

        await Build(handler).GetFuelStatusAsync();

        Assert.DoesNotContain(handler.Requests, r => Is(r, ConnectionsPath));
    }

    [Theory]
    [InlineData]
    [InlineData("veh-1", "veh-2")]
    public async Task Returns_null_when_vehicle_id_unset_and_connections_are_not_exactly_one(params string[] vehicleIds)
    {
        var handler = Handler(connections: _ => Json(HttpStatusCode.OK, ConnectionsJson(vehicleIds)));
        var options = DefaultOptions();
        options.VehicleId = null;

        var status = await Build(handler, options).GetFuelStatusAsync();

        Assert.Null(status);
        Assert.DoesNotContain(handler.Requests, r => Is(r, FuelLevelPath));
    }

    [Fact]
    public async Task Returns_null_when_connection_lookup_fails()
    {
        var handler = Handler(connections: _ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var options = DefaultOptions();
        options.VehicleId = null;

        var status = await Build(handler, options).GetFuelStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task Low_v3_reading_drives_the_alert_pipeline_end_to_end()
    {
        // Smartcar provider -> FuelAlertService state machine -> email, with only HTTP mocked.
        var handler = Handler(
            fuelLevel: _ => Json(HttpStatusCode.OK, SignalJson("internalcombustionengine-fuellevel", 18, "percent")),
            range: _ => Json(HttpStatusCode.OK, SignalJson("internalcombustionengine-range", 64.37376, "km")));
        var email = new RecordingEmailService();
        var state = new InMemoryAlertStateService();
        var alerts = new FuelAlertService(
            Build(handler),
            email,
            state,
            Options.Create(new FuelAlertOptions()),
            new MutableTimeProvider(Now),
            NullLogger<FuelAlertService>.Instance);

        var first = await alerts.EvaluateAsync();
        var second = await alerts.EvaluateAsync();

        Assert.True(first.AlertSent);
        Assert.False(second.AlertSent); // duplicate suppression still applies
        var message = Assert.Single(email.Sent);
        Assert.Contains("18%", message.Body);
        Assert.Equal(18d, state.Current!.LastKnownPercent);
        Assert.Equal(40d, state.Current.LastKnownRangeMiles!.Value, precision: 3);
    }
}
