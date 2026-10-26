using FordFuelAlert.Functions.Models;
using FordFuelAlert.Functions.Services;
using FordFuelAlert.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FordFuelAlert.Tests;

public class FuelServiceFactoryTests
{
    private static FordConnectOptions ValidFordConnect() => new()
    {
        ClientId = "client-1",
        ClientSecret = "secret-1",
        TokenStoreConnectionString = "UseDevelopmentStorage=true",
    };

    private static SmartcarOptions ValidSmartcar() => new()
    {
        ClientId = "client-1",
        ClientSecret = "secret-1",
        UserId = "user-1",
    };

    private static FuelServiceFactory.Dependencies Deps() => new(
        new SingleHandlerHttpClientFactory(new ScriptedHttpMessageHandler(_ => new HttpResponseMessage())),
        TimeProvider.System,
        NullLoggerFactory.Instance);

    [Fact]
    public void Defaults_to_fake_provider()
    {
        var service = FuelServiceFactory.Create(
            new FuelOptions(), new FuelAlertOptions(), new FakeFuelOptions(), ValidSmartcar(), ValidFordConnect(), Deps());

        Assert.IsType<FakeFuelService>(service);
    }

    [Fact]
    public void Builds_smartcar_provider()
    {
        var options = new FuelOptions { Provider = FuelOptions.SmartcarProvider };

        var service = FuelServiceFactory.Create(options, new FuelAlertOptions(), new FakeFuelOptions(), ValidSmartcar(), ValidFordConnect(), Deps());

        Assert.IsType<SmartcarFuelService>(service);
    }

    [Fact]
    public void Builds_fordconnect_provider_when_fully_configured()
    {
        var options = new FuelOptions { Provider = FuelOptions.FordConnectProvider };

        var service = FuelServiceFactory.Create(options, new FuelAlertOptions(), new FakeFuelOptions(), ValidSmartcar(), ValidFordConnect(), Deps());

        Assert.IsType<FordConnectFuelService>(service);
    }

    [Fact]
    public void Builds_fordconnect_provider_with_token_store_service_uri()
    {
        var options = new FuelOptions { Provider = FuelOptions.FordConnectProvider };
        var fordConnect = ValidFordConnect();
        fordConnect.TokenStoreConnectionString = null;
        fordConnect.TokenStoreServiceUri = "https://example.table.core.windows.net";

        var service = FuelServiceFactory.Create(options, new FuelAlertOptions(), new FakeFuelOptions(), ValidSmartcar(), fordConnect, Deps());

        Assert.IsType<FordConnectFuelService>(service);
    }

    [Fact]
    public void Ignores_provider_casing()
    {
        var options = new FuelOptions { Provider = "FordConnect" };

        var service = FuelServiceFactory.Create(options, new FuelAlertOptions(), new FakeFuelOptions(), ValidSmartcar(), ValidFordConnect(), Deps());

        Assert.IsType<FordConnectFuelService>(service);
    }

    [Fact]
    public void Throws_when_fordconnect_client_credentials_missing()
    {
        var options = new FuelOptions { Provider = FuelOptions.FordConnectProvider };
        var fordConnect = ValidFordConnect();
        fordConnect.ClientSecret = null;

        var ex = Assert.Throws<InvalidOperationException>(
            () => FuelServiceFactory.Create(options, new FuelAlertOptions(), new FakeFuelOptions(), ValidSmartcar(), fordConnect, Deps()));

        Assert.Contains("FordConnect__ClientId", ex.Message);
        Assert.Contains("FordConnect__ClientSecret", ex.Message);
    }

    [Fact]
    public void Throws_when_fordconnect_token_store_has_no_connection_info()
    {
        var options = new FuelOptions { Provider = FuelOptions.FordConnectProvider };
        var fordConnect = ValidFordConnect();
        fordConnect.TokenStoreConnectionString = null;

        var ex = Assert.Throws<InvalidOperationException>(
            () => FuelServiceFactory.Create(options, new FuelAlertOptions(), new FakeFuelOptions(), ValidSmartcar(), fordConnect, Deps()));

        Assert.Contains("FordConnect__TokenStoreConnectionString", ex.Message);
        Assert.Contains("FordConnect__TokenStoreServiceUri", ex.Message);
    }

    [Fact]
    public void Throws_on_unknown_provider()
    {
        var options = new FuelOptions { Provider = "obd" };

        var ex = Assert.Throws<InvalidOperationException>(
            () => FuelServiceFactory.Create(options, new FuelAlertOptions(), new FakeFuelOptions(), ValidSmartcar(), ValidFordConnect(), Deps()));

        Assert.Contains("Unknown fuel provider 'obd'", ex.Message);
        Assert.Contains("Fuel__Provider", ex.Message);
    }
}
