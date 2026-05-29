namespace FordFuelAlert.Functions.Models;

public sealed class FuelAlertResult
{
    public bool AlertSent { get; init; }

    public bool StateReset { get; init; }

    public bool DataUnavailable { get; init; }

    public string? Reason { get; init; }
}
