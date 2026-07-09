using FordFuelAlert.Functions.Models;
using FordFuelAlert.Functions.Services;
using FordFuelAlert.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FordFuelAlert.Tests;

public class TableAlertStateServiceTests
{
    private static TableAlertStateService CreateService(FakeTableClient tableClient, string? vehicleId = "my-ford")
    {
        return new TableAlertStateService(tableClient, vehicleId, NullLogger<TableAlertStateService>.Instance);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNoStateHasBeenSaved()
    {
        var service = CreateService(new FakeTableClient());

        var state = await service.GetAsync();

        Assert.Null(state);
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsAllFields()
    {
        var service = CreateService(new FakeTableClient());
        var saved = new AlertState
        {
            AlertSent = true,
            LastAlertSentUtc = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero),
            LastCheckedUtc = new DateTimeOffset(2026, 7, 9, 14, 30, 0, TimeSpan.Zero),
            LastKnownPercent = 18.5,
            LastKnownRangeMiles = 62.0,
        };

        await service.SaveAsync(saved);
        var loaded = await service.GetAsync();

        Assert.NotNull(loaded);
        Assert.True(loaded.AlertSent);
        Assert.Equal(saved.LastAlertSentUtc, loaded.LastAlertSentUtc);
        Assert.Equal(saved.LastCheckedUtc, loaded.LastCheckedUtc);
        Assert.Equal(saved.LastKnownPercent, loaded.LastKnownPercent);
        Assert.Equal(saved.LastKnownRangeMiles, loaded.LastKnownRangeMiles);
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_PreservesNullOptionalFields()
    {
        var service = CreateService(new FakeTableClient());
        var saved = new AlertState
        {
            AlertSent = false,
            LastAlertSentUtc = null,
            LastCheckedUtc = null,
            LastKnownPercent = null,
            LastKnownRangeMiles = null,
        };

        await service.SaveAsync(saved);
        var loaded = await service.GetAsync();

        Assert.NotNull(loaded);
        Assert.False(loaded.AlertSent);
        Assert.Null(loaded.LastAlertSentUtc);
        Assert.Null(loaded.LastCheckedUtc);
        Assert.Null(loaded.LastKnownPercent);
        Assert.Null(loaded.LastKnownRangeMiles);
    }

    [Fact]
    public async Task SaveAsync_Twice_OverwritesThePreviousState()
    {
        var tableClient = new FakeTableClient();
        var service = CreateService(tableClient);

        await service.SaveAsync(new AlertState { AlertSent = true, LastKnownPercent = 20 });
        await service.SaveAsync(new AlertState { AlertSent = false, LastKnownPercent = 55 });
        var loaded = await service.GetAsync();

        Assert.NotNull(loaded);
        Assert.False(loaded.AlertSent);
        Assert.Equal(55, loaded.LastKnownPercent);
        Assert.Single(tableClient.Entities);
    }

    [Fact]
    public async Task SaveAsync_KeysTheEntityByVehicleId()
    {
        var tableClient = new FakeTableClient();
        var service = CreateService(tableClient, vehicleId: "my-ford");

        await service.SaveAsync(new AlertState { AlertSent = true });

        var key = Assert.Single(tableClient.Entities.Keys);
        Assert.Equal("my-ford", key.RowKey);
    }

    [Fact]
    public async Task SaveAsync_FallsBackToDefaultRowKey_WhenVehicleIdMissing()
    {
        var tableClient = new FakeTableClient();
        var service = CreateService(tableClient, vehicleId: null);

        await service.SaveAsync(new AlertState { AlertSent = true });

        var key = Assert.Single(tableClient.Entities.Keys);
        Assert.Equal("default", key.RowKey);
    }

    [Fact]
    public async Task EnsuresTheTableExists_OnlyOnce_AcrossOperations()
    {
        var tableClient = new FakeTableClient();
        var service = CreateService(tableClient);

        await service.SaveAsync(new AlertState { AlertSent = true });
        await service.GetAsync();
        await service.GetAsync();

        Assert.Equal(1, tableClient.CreateIfNotExistsCallCount);
    }
}
