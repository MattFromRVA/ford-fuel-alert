namespace FordFuelAlert.Functions.Models;

public sealed class FakeFuelOptions
{
    public const string SectionName = "FakeFuel";

    public double? PercentRemaining { get; set; }

    public double? RangeMiles { get; set; }
}
