using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;

namespace FordFuelAlert.Tests.Fakes;

// In-memory stand-in for Azure Table Storage, using the Azure SDK's mockable virtual members.
internal sealed class FakeTableClient : TableClient
{
    private readonly Dictionary<(string PartitionKey, string RowKey), TableEntity> _entities = new();

    public int CreateIfNotExistsCallCount { get; private set; }

    public IReadOnlyDictionary<(string PartitionKey, string RowKey), TableEntity> Entities => _entities;

    public override Task<Response<TableItem>> CreateIfNotExistsAsync(CancellationToken cancellationToken = default)
    {
        CreateIfNotExistsCallCount++;
        return Task.FromResult<Response<TableItem>>(null!);
    }

    public override Task<Response<T>> GetEntityAsync<T>(
        string partitionKey,
        string rowKey,
        IEnumerable<string>? select = null,
        CancellationToken cancellationToken = default)
    {
        if (!_entities.TryGetValue((partitionKey, rowKey), out var entity))
        {
            throw new RequestFailedException(404, "The specified resource does not exist.", "ResourceNotFound", null);
        }

        return Task.FromResult(Response.FromValue((T)(object)entity, null!));
    }

    public override Task<Response> UpsertEntityAsync<T>(
        T entity,
        TableUpdateMode mode = TableUpdateMode.Merge,
        CancellationToken cancellationToken = default)
    {
        var tableEntity = (TableEntity)(object)entity;
        _entities[(tableEntity.PartitionKey, tableEntity.RowKey)] = tableEntity;
        return Task.FromResult<Response>(null!);
    }
}
