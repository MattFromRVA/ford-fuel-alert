using System.Net;
using System.Text;
using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;
using FordFuelAlert.Functions.Services;
using FordFuelAlert.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FordFuelAlert.Tests;

public class SmartcarFuelServiceTests
{
    private const string AuthUrl = "https://auth.smartcar.com/oauth/token";
    private const string FuelPath = "/vehicles/veh-1/fuel";

    private static string TokenJson(string accessToken, string refreshToken, int expiresIn = 7200) =>
        $$"""{"access_token":"{{accessToken}}","token_type":"Bearer","expires_in":{{expiresIn}},"refresh_token":"{{refreshToken}}"}""";

    private static string FuelJson(double percentRemaining, double amountRemaining, double range) =>
        $$"""{"percentRemaining":{{percentRemaining}},"amountRemaining":{{amountRemaining}},"range":{{range}}}""";

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static SmartcarFuelService Build(
        ScriptedHttpMessageHandler handler,
        ISmartcarTokenStore tokenStore,
        SmartcarOptions? options = null)
    {
        options ??= new SmartcarOptions
        {
            ClientId = "client-1",
            ClientSecret = "secret-1",
            VehicleId = "veh-1",
        };

        return new SmartcarFuelService(
            new SingleHandlerHttpClientFactory(handler),
            tokenStore,
            Options.Create(options),
            TimeProvider.System,
            NullLogger<SmartcarFuelService>.Instance);
    }

    // Routes the two Smartcar calls: token exchange (auth host) and the fuel endpoint (api host).
    private static ScriptedHttpMessageHandler HappyHandler(
        string newRefreshToken = "refresh-2",
        double percent = 0.4,
        double range = 150) =>
        new(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url == AuthUrl)
            {
                return Json(HttpStatusCode.OK, TokenJson("access-1", newRefreshToken));
            }

            if (url.EndsWith(FuelPath, StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, FuelJson(percent, 20, range));
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

    [Fact]
    public async Task Happy_path_maps_fuel_status_and_scales_percent()
    {
        var handler = HappyHandler(percent: 0.4, range: 150);
        var store = new InMemorySmartcarTokenStore("refresh-1");

        var status = await Build(handler, store).GetFuelStatusAsync();

        Assert.NotNull(status);
        Assert.Equal(40d, status!.PercentRemaining);   // 0.4 (0-1) scaled to 0-100
        Assert.Equal(150d, status.RangeMiles);
        Assert.Equal("veh-1", status.VehicleId);
    }

    [Fact]
    public async Task Persists_rotated_refresh_token()
    {
        var handler = HappyHandler(newRefreshToken: "refresh-2");
        var store = new InMemorySmartcarTokenStore("refresh-1");

        await Build(handler, store).GetFuelStatusAsync();

        Assert.Equal("refresh-2", store.Current);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Uses_config_refresh_token_to_seed_empty_store()
    {
        var handler = HappyHandler();
        var store = new InMemorySmartcarTokenStore(initial: null);
        var options = new SmartcarOptions
        {
            ClientId = "client-1",
            ClientSecret = "secret-1",
            VehicleId = "veh-1",
            RefreshToken = "seed-token",
        };

        await Build(handler, store, options).GetFuelStatusAsync();

        var body = handler.BodyFor(AuthUrl);
        Assert.NotNull(body);
        Assert.Contains("refresh_token=seed-token", body);
    }

    [Fact]
    public async Task Sends_basic_auth_on_token_call_and_bearer_imperial_on_fuel_call()
    {
        var handler = HappyHandler();
        var store = new InMemorySmartcarTokenStore("refresh-1");

        await Build(handler, store).GetFuelStatusAsync();

        var authRequest = handler.Requests.Single(r => r.RequestUri!.ToString() == AuthUrl);
        Assert.Equal("Basic", authRequest.Headers.Authorization!.Scheme);

        var fuelRequest = handler.Requests.Single(r => r.RequestUri!.ToString().EndsWith(FuelPath, StringComparison.Ordinal));
        Assert.Equal("Bearer", fuelRequest.Headers.Authorization!.Scheme);
        Assert.Equal("access-1", fuelRequest.Headers.Authorization.Parameter);
        Assert.Equal("imperial", fuelRequest.Headers.GetValues("SC-Unit-System").Single());
    }

    [Fact]
    public async Task Returns_null_and_makes_no_fuel_call_when_token_exchange_fails()
    {
        var handler = new ScriptedHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var store = new InMemorySmartcarTokenStore("refresh-1");

        var status = await Build(handler, store).GetFuelStatusAsync();

        Assert.Null(status);
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.ToString().EndsWith(FuelPath, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Returns_null_when_fuel_call_returns_error_status()
    {
        var handler = new ScriptedHttpMessageHandler(request =>
            request.RequestUri!.ToString() == AuthUrl
                ? Json(HttpStatusCode.OK, TokenJson("access-1", "refresh-2"))
                : new HttpResponseMessage((HttpStatusCode)429));
        var store = new InMemorySmartcarTokenStore("refresh-1");

        var status = await Build(handler, store).GetFuelStatusAsync();

        Assert.Null(status);
        // The rotated token was already persisted during the (successful) token exchange.
        Assert.Equal("refresh-2", store.Current);
    }

    [Fact]
    public async Task Returns_null_on_transport_exception()
    {
        var handler = new ScriptedHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString() == AuthUrl)
            {
                return Json(HttpStatusCode.OK, TokenJson("access-1", "refresh-2"));
            }

            throw new HttpRequestException("connection reset");
        });
        var store = new InMemorySmartcarTokenStore("refresh-1");

        var status = await Build(handler, store).GetFuelStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task Returns_null_on_malformed_fuel_json()
    {
        var handler = new ScriptedHttpMessageHandler(request =>
            request.RequestUri!.ToString() == AuthUrl
                ? Json(HttpStatusCode.OK, TokenJson("access-1", "refresh-2"))
                : Json(HttpStatusCode.OK, "this is not json"));
        var store = new InMemorySmartcarTokenStore("refresh-1");

        var status = await Build(handler, store).GetFuelStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task Returns_null_and_makes_no_http_calls_when_no_refresh_token_available()
    {
        var handler = new ScriptedHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var store = new InMemorySmartcarTokenStore(initial: null);
        var options = new SmartcarOptions { ClientId = "client-1", ClientSecret = "secret-1", VehicleId = "veh-1" };

        var status = await Build(handler, store, options).GetFuelStatusAsync();

        Assert.Null(status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Caches_access_token_across_calls()
    {
        var handler = HappyHandler();
        var store = new InMemorySmartcarTokenStore("refresh-1");
        var service = Build(handler, store);

        await service.GetFuelStatusAsync();
        await service.GetFuelStatusAsync();

        Assert.Single(handler.Requests, r => r.RequestUri!.ToString() == AuthUrl);
        Assert.Equal(2, handler.Requests.Count(r => r.RequestUri!.ToString().EndsWith(FuelPath, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Returns_null_when_token_store_read_throws()
    {
        // Transient Azure Table Storage failure while reading the stored token must degrade to null,
        // not crash the timer run (the "every failure path degrades to null" contract).
        var handler = HappyHandler();
        var store = new ThrowingSmartcarTokenStore(throwOnGet: true);

        var status = await Build(handler, store).GetFuelStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task Returns_null_when_token_store_save_throws()
    {
        // The token exchange succeeded but persisting the rotated token failed — must still degrade
        // to null rather than let the Table Storage exception crash the run.
        var handler = HappyHandler();
        var store = new ThrowingSmartcarTokenStore(throwOnSave: true);

        var status = await Build(handler, store).GetFuelStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task Invalidates_cached_access_token_on_unauthorized_fuel_response()
    {
        // A 401 means the cached token is dead server-side; the next run must re-exchange rather
        // than reuse it for the whole cache window (else no fuel is read for up to ~2 hours).
        var handler = new ScriptedHttpMessageHandler(request =>
            request.RequestUri!.ToString() == AuthUrl
                ? Json(HttpStatusCode.OK, TokenJson("access-1", "refresh-2"))
                : new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var store = new InMemorySmartcarTokenStore("refresh-1");
        var service = Build(handler, store);

        await service.GetFuelStatusAsync();
        await service.GetFuelStatusAsync();

        Assert.Equal(2, handler.Requests.Count(r => r.RequestUri!.ToString() == AuthUrl));
    }

    [Fact]
    public async Task Returns_null_when_percent_remaining_is_absent()
    {
        var handler = new ScriptedHttpMessageHandler(request =>
            request.RequestUri!.ToString() == AuthUrl
                ? Json(HttpStatusCode.OK, TokenJson("access-1", "refresh-2"))
                : Json(HttpStatusCode.OK, """{"percentRemaining":null,"amountRemaining":null,"range":null}"""));
        var store = new InMemorySmartcarTokenStore("refresh-1");

        var status = await Build(handler, store).GetFuelStatusAsync();

        // Data-unavailable (European vehicle / manufacturer gap): a FuelStatus with null percent,
        // which the alert logic already treats as "no decision".
        Assert.NotNull(status);
        Assert.Null(status!.PercentRemaining);
    }
}
