using FordFuelAlert.Functions.Models;
using FordFuelAlert.Functions.Services;
using FordFuelAlert.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FordFuelAlert.Tests;

public class FordConnectTokenStoreTests
{
    private static FordConnectTokenStore CreateStore(FakeTableClient tableClient) =>
        new(tableClient, NullLogger<FordConnectTokenStore>.Instance);

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNoTokenHasBeenSaved()
    {
        var store = CreateStore(new FakeTableClient());

        var token = await store.GetAsync();

        Assert.Null(token);
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsAllFields()
    {
        var store = CreateStore(new FakeTableClient());
        var saved = new FordConnectToken
        {
            RefreshToken = "refresh-2",
            AuthTimeUtc = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero),
            LastRefreshedUtc = new DateTimeOffset(2026, 10, 26, 11, 0, 0, TimeSpan.Zero),
        };

        await store.SaveAsync(saved);
        var loaded = await store.GetAsync();

        Assert.NotNull(loaded);
        Assert.Equal("refresh-2", loaded.RefreshToken);
        Assert.Equal(saved.AuthTimeUtc, loaded.AuthTimeUtc);
        Assert.Equal(saved.LastRefreshedUtc, loaded.LastRefreshedUtc);
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_PreservesNullOptionalFields()
    {
        var store = CreateStore(new FakeTableClient());

        await store.SaveAsync(new FordConnectToken { RefreshToken = "refresh-1" });
        var loaded = await store.GetAsync();

        Assert.NotNull(loaded);
        Assert.Null(loaded.AuthTimeUtc);
        Assert.Null(loaded.LastRefreshedUtc);
    }

    [Fact]
    public async Task SaveAsync_Twice_OverwritesThePreviousToken()
    {
        var tableClient = new FakeTableClient();
        var store = CreateStore(tableClient);

        await store.SaveAsync(new FordConnectToken { RefreshToken = "refresh-1" });
        await store.SaveAsync(new FordConnectToken { RefreshToken = "refresh-2" });
        var loaded = await store.GetAsync();

        Assert.Equal("refresh-2", loaded!.RefreshToken);
        Assert.Single(tableClient.Entities);
    }

    [Fact]
    public async Task EnsuresTableExistsOnlyOnce()
    {
        var tableClient = new FakeTableClient();
        var store = CreateStore(tableClient);

        await store.GetAsync();
        await store.SaveAsync(new FordConnectToken { RefreshToken = "refresh-1" });
        await store.GetAsync();

        Assert.Equal(1, tableClient.CreateIfNotExistsCallCount);
    }
}
