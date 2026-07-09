using Azure.Data.Tables;
using Azure.Identity;
using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;
using Microsoft.Extensions.Logging;

namespace FordFuelAlert.Functions.Services;

// Builds the IAlertStateService selected by AlertState__Provider ("file" or "table").
public static class AlertStateServiceFactory
{
    public static IAlertStateService Create(AlertStateOptions options, string? vehicleId, ILoggerFactory loggerFactory)
    {
        if (string.Equals(options.Provider, AlertStateOptions.FileProvider, StringComparison.OrdinalIgnoreCase))
        {
            return new FileAlertStateService(
                ResolveFilePath(options.FilePath),
                loggerFactory.CreateLogger<FileAlertStateService>());
        }

        if (string.Equals(options.Provider, AlertStateOptions.TableProvider, StringComparison.OrdinalIgnoreCase))
        {
            return new TableAlertStateService(
                CreateTableClient(options),
                vehicleId,
                loggerFactory.CreateLogger<TableAlertStateService>());
        }

        throw new InvalidOperationException(
            $"Unknown alert state provider '{options.Provider}'. "
            + $"Set AlertState__Provider to '{AlertStateOptions.FileProvider}' or '{AlertStateOptions.TableProvider}'.");
    }

    private static TableClient CreateTableClient(AlertStateOptions options)
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
            "AlertState__Provider is 'table' but neither AlertState__ConnectionString nor AlertState__ServiceUri is set.");
    }

    private static string ResolveFilePath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ford-fuel-alert",
            "alert-state.json");
    }
}
