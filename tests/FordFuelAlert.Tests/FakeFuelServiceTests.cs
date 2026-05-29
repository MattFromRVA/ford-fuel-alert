using FordFuelAlert.Functions.Models;
using FordFuelAlert.Functions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FordFuelAlert.Tests;

public class FakeFuelServiceTests
{
    [Fact]
    public async Task Returns_status_from_configuration()
    {
        var service = new FakeFuelService(
            Options.Create(new FakeFuelOptions { PercentRemaining = 33, RangeMiles = 120 }),
            Options.Create(new FuelAlertOptions { VehicleId = "vehicle-123" }),
            TimeProvider.System,
            NullLogger<FakeFuelService>.Instance);

        var status = await service.GetFuelStatusAsync();

        Assert.NotNull(status);
        Assert.Equal(33d, status!.PercentRemaining);
        Assert.Equal(120d, status.RangeMiles);
        Assert.Equal("vehicle-123", status.VehicleId);
    }

    [Fact]
    public async Task Returns_null_percent_when_not_configured()
    {
        var service = new FakeFuelService(
            Options.Create(new FakeFuelOptions()),
            Options.Create(new FuelAlertOptions()),
            TimeProvider.System,
            NullLogger<FakeFuelService>.Instance);

        var status = await service.GetFuelStatusAsync();

        Assert.NotNull(status);
        Assert.Null(status!.PercentRemaining);
    }
}
