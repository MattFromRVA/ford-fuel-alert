using Azure;
using Azure.Data.Tables;
using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;
using Microsoft.Extensions.Logging;

namespace FordFuelAlert.Functions.Services;

// Durable IAlertStateService for Azure: persists a single AlertState entity in Table Storage,
// replacing the local FileAlertStateService (whose file system is ephemeral in Azure).
// One entity per vehicle: PartitionKey is fixed, RowKey is the vehicle id.
public sealed class TableAlertStateService : IAlertStateService
{
    private const string PartitionKey = "fuel-alert";
    private const string DefaultRowKey = "default";

    private readonly TableClient _tableClient;
    private readonly string _rowKey;
    private readonly ILogger<TableAlertStateService> _logger;
    private bool _tableEnsured;

    public TableAlertStateService(TableClient tableClient, string? vehicleId, ILogger<TableAlertStateService> logger)
    {
        _tableClient = tableClient;
        _rowKey = string.IsNullOrWhiteSpace(vehicleId) ? DefaultRowKey : SanitizeKey(vehicleId);
        _logger = logger;
    }

    // Azure Table Storage rejects '/', '\', '#', '?', and control characters in PartitionKey/RowKey
    // values; FuelAlert__VehicleId is free-form config, so map those to '-' instead of failing every run.
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

    public async Task<AlertState?> GetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);

        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(PartitionKey, _rowKey, select: null, cancellationToken);
            var entity = response.Value;

            return new AlertState
            {
                AlertSent = entity.GetBoolean(nameof(AlertState.AlertSent)) ?? false,
                LastAlertSentUtc = entity.GetDateTimeOffset(nameof(AlertState.LastAlertSentUtc)),
                LastCheckedUtc = entity.GetDateTimeOffset(nameof(AlertState.LastCheckedUtc)),
                LastKnownPercent = entity.GetDouble(nameof(AlertState.LastKnownPercent)),
                LastKnownRangeMiles = entity.GetDouble(nameof(AlertState.LastKnownRangeMiles)),
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation("No existing alert state in table storage for {RowKey}; starting fresh.", _rowKey);
            return null;
        }
    }

    public async Task SaveAsync(AlertState state, CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);

        var entity = new TableEntity(PartitionKey, _rowKey)
        {
            [nameof(AlertState.AlertSent)] = state.AlertSent,
            [nameof(AlertState.LastAlertSentUtc)] = state.LastAlertSentUtc,
            [nameof(AlertState.LastCheckedUtc)] = state.LastCheckedUtc,
            [nameof(AlertState.LastKnownPercent)] = state.LastKnownPercent,
            [nameof(AlertState.LastKnownRangeMiles)] = state.LastKnownRangeMiles,
        };

        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        _logger.LogInformation("Saved alert state to table storage for {RowKey} (AlertSent={AlertSent}).", _rowKey, state.AlertSent);
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
