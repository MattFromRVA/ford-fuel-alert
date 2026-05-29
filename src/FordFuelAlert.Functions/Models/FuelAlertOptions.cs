namespace FordFuelAlert.Functions.Models;

public sealed class FuelAlertOptions
{
    public const string SectionName = "FuelAlert";

    public double ThresholdPercent { get; set; } = 25;

    public double ResetPercent { get; set; } = 40;

    public string? RecipientEmail { get; set; }

    public string? VehicleId { get; set; }

    public bool UseFakeFuelService { get; set; } = true;
}
