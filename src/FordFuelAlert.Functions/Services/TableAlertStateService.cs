using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;

namespace FordFuelAlert.Functions.Services;

// PLACEHOLDER — not implemented and not registered. A durable IAlertStateService for Azure,
// intended to replace the local FileAlertStateService once deployed.
//
// TODO (future Azure Table Storage integration):
//   1. Add the Azure.Data.Tables package.
//   2. Create a TableClient from a connection string or managed identity (config / Key Vault).
//   3. Persist a single AlertState entity with a fixed PartitionKey/RowKey (e.g. the VehicleId).
//   4. Map AlertState <-> ITableEntity in GetAsync / SaveAsync.
public sealed class TableAlertStateService : IAlertStateService
{
    public Task<AlertState?> GetAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("TableAlertStateService is a placeholder. Use FileAlertStateService for local development.");
    }

    public Task SaveAsync(AlertState state, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("TableAlertStateService is a placeholder. Use FileAlertStateService for local development.");
    }
}
