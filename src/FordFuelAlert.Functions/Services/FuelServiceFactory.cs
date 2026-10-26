using Azure.Data.Tables;
using Azure.Identity;
using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FordFuelAlert.Functions.Services;

public static class FuelServiceFactory
{
    public sealed record Dependencies(
        IHttpClientFactory HttpClientFactory,
        TimeProvider TimeProvider,
        ILoggerFactory LoggerFactory);

    public static IFuelService Create(
        FuelOptions options,
        FuelAlertOptions alertOptions,
        FakeFuelOptions fakeOptions,
        SmartcarOptions smartcarOptions,
        FordConnectOptions fordConnectOptions,
        Dependencies dependencies)
    {
        if (string.Equals(options.Provider, FuelOptions.FakeProvider, StringComparison.OrdinalIgnoreCase))
        {
            return new FakeFuelService(
                Options.Create(fakeOptions),
                Options.Create(alertOptions),
                dependencies.TimeProvider,
                dependencies.LoggerFactory.CreateLogger<FakeFuelService>());
        }

        if (string.Equals(options.Provider, FuelOptions.SmartcarProvider, StringComparison.OrdinalIgnoreCase))
        {
            return new SmartcarFuelService(
                dependencies.HttpClientFactory,
                Options.Create(smartcarOptions),
                dependencies.TimeProvider,
                dependencies.LoggerFactory.CreateLogger<SmartcarFuelService>());
        }

        if (string.Equals(options.Provider, FuelOptions.FordConnectProvider, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(fordConnectOptions.ClientId) || string.IsNullOrWhiteSpace(fordConnectOptions.ClientSecret))
            {
                throw new InvalidOperationException(
                    "Fuel__Provider is 'fordconnect' but FordConnect__ClientId or FordConnect__ClientSecret is not set. "
                    + "Both come from the application registered on developer.ford.com.");
            }

            var tokenStore = new FordConnectTokenStore(
                CreateTokenTableClient(fordConnectOptions),
                dependencies.LoggerFactory.CreateLogger<FordConnectTokenStore>());

            return new FordConnectFuelService(
                dependencies.HttpClientFactory,
                Options.Create(fordConnectOptions),
                tokenStore,
                dependencies.TimeProvider,
                dependencies.LoggerFactory.CreateLogger<FordConnectFuelService>());
        }

        throw new InvalidOperationException(
            $"Unknown fuel provider '{options.Provider}'. "
            + $"Set Fuel__Provider to '{FuelOptions.FakeProvider}', '{FuelOptions.SmartcarProvider}' or '{FuelOptions.FordConnectProvider}'.");
    }

    private static TableClient CreateTokenTableClient(FordConnectOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.TokenStoreConnectionString))
        {
            return new TableClient(options.TokenStoreConnectionString, options.TokenStoreTableName);
        }

        if (!string.IsNullOrWhiteSpace(options.TokenStoreServiceUri))
        {
            return new TableClient(new Uri(options.TokenStoreServiceUri), options.TokenStoreTableName, new DefaultAzureCredential());
        }

        throw new InvalidOperationException(
            "Fuel__Provider is 'fordconnect' but neither FordConnect__TokenStoreConnectionString nor FordConnect__TokenStoreServiceUri is set.");
    }
}
