using FordFuelAlert.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FordFuelAlert.Functions.Functions;

// Thin trigger: all decision logic lives in FuelAlertService.
public sealed class CheckFuelLevelFunction
{
    private readonly FuelAlertService _fuelAlertService;
    private readonly ILogger<CheckFuelLevelFunction> _logger;

    public CheckFuelLevelFunction(FuelAlertService fuelAlertService, ILogger<CheckFuelLevelFunction> logger)
    {
        _fuelAlertService = fuelAlertService;
        _logger = logger;
    }

    // Runs every 2 hours. To change cadence, edit this NCRONTAB expression (e.g. "0 0 */4 * * *" for every 4 hours),
    // or bind it to an app setting via [TimerTrigger("%FuelAlertSchedule%")].
    [Function(nameof(CheckFuelLevelFunction))]
    public async Task Run([TimerTrigger("0 0 */2 * * *")] TimerInfo timerInfo, CancellationToken cancellationToken)
    {
        _logger.LogInformation("CheckFuelLevelFunction started at {Utc:o}.", DateTimeOffset.UtcNow);

        var result = await _fuelAlertService.EvaluateAsync(cancellationToken);

        _logger.LogInformation(
            "CheckFuelLevelFunction finished. AlertSent={AlertSent} StateReset={StateReset} DataUnavailable={DataUnavailable}.",
            result.AlertSent, result.StateReset, result.DataUnavailable);
    }
}
