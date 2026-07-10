using Azure;
using Azure.Data.Tables;
using FordFuelAlert.Functions.Abstractions;
using Microsoft.Extensions.Logging;

namespace FordFuelAlert.Functions.Services;

// Durable ISmartcarTokenStore for Azure: persists the rotating Smartcar refresh token as a single
// entity in Table Storage. Mirrors TableAlertStateService — one entity per vehicle, fixed
// PartitionKey, RowKey is the (sanitized) vehicle id.
public sealed class TableSmartcarTokenStore : ISmartcarTokenStore
{
    private const string PartitionKey = "smartcar-token";
    private const string DefaultRowKey = "default";
    private const string RefreshTokenColumn = "RefreshToken";

    private readonly TableClient _tableClient;
    private readonly string _rowKey;
    private readonly ILogger<TableSmartcarTokenStore> _logger;
    private bool _tableEnsured;

    public TableSmartcarTokenStore(TableClient tableClient, string? vehicleId, ILogger<TableSmartcarTokenStore> logger)
    {
        _tableClient = tableClient;
        _rowKey = string.IsNullOrWhiteSpace(vehicleId) ? DefaultRowKey : SanitizeKey(vehicleId);
        _logger = logger;
    }

    // Azure Table Storage rejects '/', '\', '#', '?', and control characters in PartitionKey/RowKey
    // values; the vehicle id is free-form config, so map those to '-' instead of failing every run.
    private static string SanitizeKey(string value)
    {
        var characters = value.ToCharArray();
        for (var i = 0; i < characters.Length; i++)
        {
            if (characters[i] is '/' or '\\' or '#' or '?' || char.IsControl(characters[i]))
            {
                characters[i] = '-';
            }
        }

        return new string(characters);
    }

    public async Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);

        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(PartitionKey, _rowKey, select: null, cancellationToken);
            return response.Value.GetString(RefreshTokenColumn);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation("No stored Smartcar refresh token for {RowKey}; will fall back to configured bootstrap token.", _rowKey);
            return null;
        }
    }

    public async Task SaveRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);

        var entity = new TableEntity(PartitionKey, _rowKey)
        {
            [RefreshTokenColumn] = refreshToken,
        };

        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        // Never log the token value — only that a rotation was persisted.
        _logger.LogInformation("Persisted rotated Smartcar refresh token for {RowKey}.", _rowKey);
    }

    private async Task EnsureTableExistsAsync(CancellationToken cancellationToken)
    {
        // The function host runs timer executions sequentially on this singleton, so a simple flag suffices.
        if (_tableEnsured)
        {
            return;
        }

        await _tableClient.CreateIfNotExistsAsync(cancellationToken);
        _tableEnsured = true;
    }
}
