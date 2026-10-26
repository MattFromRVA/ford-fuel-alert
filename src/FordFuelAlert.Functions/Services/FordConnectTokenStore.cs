using Azure;
using Azure.Data.Tables;
using FordFuelAlert.Functions.Models;
using Microsoft.Extensions.Logging;

namespace FordFuelAlert.Functions.Services;

public sealed class FordConnectTokenStore
{
    private const string PartitionKey = "fordconnect";
    private const string RowKey = "default";

    private readonly TableClient _tableClient;
    private readonly ILogger<FordConnectTokenStore> _logger;
    private bool _tableEnsured;

    public FordConnectTokenStore(TableClient tableClient, ILogger<FordConnectTokenStore> logger)
    {
        _tableClient = tableClient;
        _logger = logger;
    }

    public async Task<FordConnectToken?> GetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);

        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(PartitionKey, RowKey, select: null, cancellationToken);
            var entity = response.Value;
            var refreshToken = entity.GetString(nameof(FordConnectToken.RefreshToken));
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return null;
            }

            return new FordConnectToken
            {
                RefreshToken = refreshToken,
                AuthTimeUtc = entity.GetDateTimeOffset(nameof(FordConnectToken.AuthTimeUtc)),
                LastRefreshedUtc = entity.GetDateTimeOffset(nameof(FordConnectToken.LastRefreshedUtc)),
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation("No stored FordConnect token yet; the seed refresh token will be used.");
            return null;
        }
    }

    public async Task SaveAsync(FordConnectToken token, CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);

        var entity = new TableEntity(PartitionKey, RowKey)
        {
            [nameof(FordConnectToken.RefreshToken)] = token.RefreshToken,
            [nameof(FordConnectToken.AuthTimeUtc)] = token.AuthTimeUtc,
            [nameof(FordConnectToken.LastRefreshedUtc)] = token.LastRefreshedUtc,
        };

        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        _logger.LogInformation("Saved FordConnect refresh token (AuthTimeUtc={AuthTimeUtc}).", token.AuthTimeUtc);
    }

    private async Task EnsureTableExistsAsync(CancellationToken cancellationToken)
    {
        if (_tableEnsured)
        {
            return;
        }

        await _tableClient.CreateIfNotExistsAsync(cancellationToken);
        _tableEnsured = true;
    }
}
