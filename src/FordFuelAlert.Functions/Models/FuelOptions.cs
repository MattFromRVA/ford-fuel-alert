namespace FordFuelAlert.Functions.Models;

public sealed class FuelOptions
{
    public const string SectionName = "Fuel";

    public const string FakeProvider = "fake";
    public const string SmartcarProvider = "smartcar";
    public const string FordConnectProvider = "fordconnect";

    public string Provider { get; set; } = FakeProvider;
}
