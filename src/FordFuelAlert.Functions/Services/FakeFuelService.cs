using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FordFuelAlert.Functions.Services;

// First-pass fuel provider: returns the values configured under FakeFuel__*. Lets us exercise the
// alert/reset/state/email pipeline end to end without any real vehicle API.
public sealed class FakeFuelService : IFuelService
{
    private readonly FakeFuelOptions _fakeOptions;
    private readonly FuelAlertOptions _alertOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FakeFuelService> _logger;

    public FakeFuelService(
        IOptions<FakeFuelOptions> fakeOptions,
        IOptions<FuelAlertOptions> alertOptions,
        TimeProvider timeProvider,
        ILogger<FakeFuelService> logger)
    {
        _fakeOptions = fakeOptions.Value;
        _alertOptions = alertOptions.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<FuelStatus?> GetFuelStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new FuelStatus
        {
            PercentRemaining = _fakeOptions.PercentRemaining,
            RangeMiles = _fakeOptions.RangeMiles,
            RetrievedAtUtc = _timeProvider.GetUtcNow(),
            VehicleId = _alertOptions.VehicleId,
        };

        _logger.LogInformation(
            "FakeFuelService returning Percent={Percent} Range={Range}. Change via FakeFuel__PercentRemaining / FakeFuel__RangeMiles.",
            status.PercentRemaining, status.RangeMiles);

        return Task.FromResult<FuelStatus?>(status);
    }
}
