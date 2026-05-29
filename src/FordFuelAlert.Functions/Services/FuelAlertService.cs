using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FordFuelAlert.Functions.Services;

// Core, provider-agnostic alert logic. Deliberately free of any Azure Functions types so it can be unit-tested directly.
public sealed class FuelAlertService
{
    private readonly IFuelService _fuelService;
    private readonly IEmailService _emailService;
    private readonly IAlertStateService _stateService;
    private readonly FuelAlertOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FuelAlertService> _logger;

    public FuelAlertService(
        IFuelService fuelService,
        IEmailService emailService,
        IAlertStateService stateService,
        IOptions<FuelAlertOptions> options,
        TimeProvider timeProvider,
        ILogger<FuelAlertService> logger)
    {
        _fuelService = fuelService;
        _emailService = emailService;
        _stateService = stateService;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<FuelAlertResult> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var state = await _stateService.GetAsync(cancellationToken) ?? new AlertState();
        state.LastCheckedUtc = now;

        var status = await _fuelService.GetFuelStatusAsync(cancellationToken);

        if (status?.PercentRemaining is null)
        {
            _logger.LogWarning("Fuel data unavailable for vehicle {VehicleId}; no alert decision made.", _options.VehicleId);
            await _stateService.SaveAsync(state, cancellationToken);
            return new FuelAlertResult { DataUnavailable = true, Reason = "Fuel data unavailable" };
        }

        var percent = status.PercentRemaining.Value;
        state.LastKnownPercent = percent;
        state.LastKnownRangeMiles = status.RangeMiles;

        var alertSent = false;
        var stateReset = false;

        if (percent <= _options.ThresholdPercent && !state.AlertSent)
        {
            if (string.IsNullOrWhiteSpace(_options.RecipientEmail))
            {
                _logger.LogWarning("FuelAlert__RecipientEmail is not set; the alert will be logged but has no real recipient.");
            }

            var message = BuildAlertEmail(percent, status.RangeMiles, status.RetrievedAtUtc);
            await _emailService.SendAsync(message, cancellationToken);

            state.AlertSent = true;
            state.LastAlertSentUtc = now;
            alertSent = true;

            _logger.LogInformation("Low-fuel alert sent: {Percent}% <= threshold {Threshold}%.", percent, _options.ThresholdPercent);
        }
        else if (percent >= _options.ResetPercent && state.AlertSent)
        {
            state.AlertSent = false;
            stateReset = true;

            _logger.LogInformation("Fuel recovered to {Percent}% >= reset {Reset}%; alert state cleared.", percent, _options.ResetPercent);
        }
        else
        {
            _logger.LogInformation(
                "No action. Percent={Percent}% Threshold={Threshold}% Reset={Reset}% AlertAlreadySent={AlertSent}.",
                percent, _options.ThresholdPercent, _options.ResetPercent, state.AlertSent);
        }

        await _stateService.SaveAsync(state, cancellationToken);

        return new FuelAlertResult { AlertSent = alertSent, StateReset = stateReset };
    }

    private EmailMessage BuildAlertEmail(double percent, double? rangeMiles, DateTimeOffset retrievedAtUtc)
    {
        var recipient = string.IsNullOrWhiteSpace(_options.RecipientEmail) ? "(recipient not configured)" : _options.RecipientEmail;
        var rangeText = rangeMiles is null ? "unavailable" : $"{rangeMiles.Value:0} miles";

        var body =
            $"Fuel is low.{Environment.NewLine}" +
            $"Fuel: {percent:0.#}%{Environment.NewLine}" +
            $"Estimated range: {rangeText}{Environment.NewLine}" +
            $"Checked at (UTC): {retrievedAtUtc:yyyy-MM-dd HH:mm:ss}";

        return new EmailMessage(recipient, "Ford fuel is low", body);
    }
}
