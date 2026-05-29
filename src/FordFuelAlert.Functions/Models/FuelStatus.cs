namespace FordFuelAlert.Functions.Models;

public sealed class FuelStatus
{
    public double? PercentRemaining { get; init; }

    public double? RangeMiles { get; init; }

    public DateTimeOffset RetrievedAtUtc { get; init; }

    public string? VehicleId { get; init; }
}
