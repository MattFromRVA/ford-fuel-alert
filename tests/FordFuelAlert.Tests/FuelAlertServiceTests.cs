using FordFuelAlert.Functions.Models;
using FordFuelAlert.Functions.Services;
using FordFuelAlert.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FordFuelAlert.Tests;

public class FuelAlertServiceTests
{
    private static (FuelAlertService Service, RecordingEmailService Email, InMemoryAlertStateService State) Build(
        FuelStatus? status,
        AlertState? initialState = null,
        FuelAlertOptions? options = null)
    {
        var email = new RecordingEmailService();
        var state = new InMemoryAlertStateService(initialState);
        var service = new FuelAlertService(
            new StubFuelService(status),
            email,
            state,
            Options.Create(options ?? new FuelAlertOptions()),
            TimeProvider.System,
            NullLogger<FuelAlertService>.Instance);
        return (service, email, state);
    }

    private static FuelStatus Fuel(double? percent, double? range = null)
    {
        return new FuelStatus
        {
            PercentRemaining = percent,
            RangeMiles = range,
            RetrievedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    [Fact]
    public async Task Sends_alert_when_below_threshold_and_not_already_sent()
    {
        var (service, email, state) = Build(Fuel(20, range: 30));

        var result = await service.EvaluateAsync();

        Assert.True(result.AlertSent);
        Assert.Single(email.Sent);
        Assert.Equal("Ford fuel is low", email.Sent[0].Subject);
        Assert.Contains("20%", email.Sent[0].Body);
        Assert.True(state.Current!.AlertSent);
        Assert.NotNull(state.Current.LastAlertSentUtc);
    }

    [Fact]
    public async Task Sends_alert_at_exact_threshold_boundary()
    {
        var (service, email, _) = Build(Fuel(25));

        var result = await service.EvaluateAsync();

        Assert.True(result.AlertSent);
        Assert.Single(email.Sent);
    }

    [Fact]
    public async Task Does_not_send_duplicate_when_alert_already_sent()
    {
        var (service, email, state) = Build(Fuel(20), initialState: new AlertState { AlertSent = true });

        var result = await service.EvaluateAsync();

        Assert.False(result.AlertSent);
        Assert.Empty(email.Sent);
        Assert.True(state.Current!.AlertSent);
    }

    [Fact]
    public async Task Resets_alert_when_fuel_at_or_above_reset_threshold()
    {
        var (service, email, state) = Build(Fuel(45), initialState: new AlertState { AlertSent = true });

        var result = await service.EvaluateAsync();

        Assert.True(result.StateReset);
        Assert.False(result.AlertSent);
        Assert.Empty(email.Sent);
        Assert.False(state.Current!.AlertSent);
    }

    [Fact]
    public async Task Does_not_reset_in_hysteresis_band()
    {
        // Between threshold (25) and reset (40): an existing alert stays active and no email is sent.
        var (service, email, state) = Build(Fuel(30), initialState: new AlertState { AlertSent = true });

        var result = await service.EvaluateAsync();

        Assert.False(result.AlertSent);
        Assert.False(result.StateReset);
        Assert.Empty(email.Sent);
        Assert.True(state.Current!.AlertSent);
    }

    [Fact]
    public async Task Does_nothing_when_fuel_above_threshold_and_no_prior_alert()
    {
        var (service, email, state) = Build(Fuel(50));

        var result = await service.EvaluateAsync();

        Assert.False(result.AlertSent);
        Assert.False(result.StateReset);
        Assert.Empty(email.Sent);
        Assert.False(state.Current!.AlertSent);
        Assert.Equal(50d, state.Current.LastKnownPercent);
    }

    [Fact]
    public async Task Handles_null_status_without_sending_or_crashing()
    {
        var (service, email, state) = Build(status: null);

        var result = await service.EvaluateAsync();

        Assert.True(result.DataUnavailable);
        Assert.False(result.AlertSent);
        Assert.Empty(email.Sent);
        Assert.NotNull(state.Current);
        Assert.NotNull(state.Current!.LastCheckedUtc);
    }

    [Fact]
    public async Task Handles_unavailable_percent_without_sending()
    {
        var (service, email, _) = Build(Fuel(percent: null));

        var result = await service.EvaluateAsync();

        Assert.True(result.DataUnavailable);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task Honors_custom_threshold_from_options()
    {
        var options = new FuelAlertOptions { ThresholdPercent = 10, ResetPercent = 20 };
        var (service, email, _) = Build(Fuel(15), options: options);

        var result = await service.EvaluateAsync();

        Assert.False(result.AlertSent); // 15 is above the custom threshold of 10
        Assert.Empty(email.Sent);
    }
}
