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

    // Schedule comes from the FuelAlertSchedule app setting (NCRONTAB; every 2 hours in the sample settings and Bicep).
    // The setting is required: the function does not load without it.
    [Function(nameof(CheckFuelLevelFunction))]
    public async Task Run([TimerTrigger("%FuelAlertSchedule%")] TimerInfo timerInfo, CancellationToken cancellationToken)
    {
        _logger.LogInformation("CheckFuelLevelFunction started at {Utc:o}.", DateTimeOffset.UtcNow);

        var result = await _fuelAlertService.EvaluateAsync(cancellationToken);

        _logger.LogInformation(
            "CheckFuelLevelFunction finished. AlertSent={AlertSent} StateReset={StateReset} DataUnavailable={DataUnavailable}.",
            result.AlertSent, result.StateReset, result.DataUnavailable);
    }
}
