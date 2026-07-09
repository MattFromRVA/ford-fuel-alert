using Azure.Data.Tables;
using Azure.Identity;
using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;
using Microsoft.Extensions.Logging;

namespace FordFuelAlert.Functions.Services;

// Builds the Table-backed ISmartcarTokenStore from Smartcar__* configuration. Mirrors the table
// branch of AlertStateServiceFactory: connection string (local Azurite / access key) or a service
// endpoint used with managed identity (preferred in Azure).
public static class SmartcarTokenStoreFactory
{
    public static ISmartcarTokenStore Create(SmartcarOptions options, ILoggerFactory loggerFactory)
    {
        return new TableSmartcarTokenStore(
            CreateTableClient(options),
            options.VehicleId,
            loggerFactory.CreateLogger<TableSmartcarTokenStore>());
    }

    private static TableClient CreateTableClient(SmartcarOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return new TableClient(options.ConnectionString, options.TableName);
        }

        if (!string.IsNullOrWhiteSpace(options.ServiceUri))
        {
            // Managed identity (or developer credentials locally) — preferred in Azure over access keys.
            return new TableClient(new Uri(options.ServiceUri), options.TableName, new DefaultAzureCredential());
        }

        throw new InvalidOperationException(
            "Smartcar is enabled but neither Smartcar__ConnectionString nor Smartcar__ServiceUri is set "
            + "for the refresh-token store.");
    }
}
