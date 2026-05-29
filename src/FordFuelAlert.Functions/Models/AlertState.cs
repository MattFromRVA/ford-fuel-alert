namespace FordFuelAlert.Functions.Models;

public sealed class AlertState
{
    public bool AlertSent { get; set; }

    public DateTimeOffset? LastAlertSentUtc { get; set; }

    public DateTimeOffset? LastCheckedUtc { get; set; }

    public double? LastKnownPercent { get; set; }

    public double? LastKnownRangeMiles { get; set; }
}
