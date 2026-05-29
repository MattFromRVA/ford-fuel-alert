namespace FordFuelAlert.Functions.Models;

// Reserved for the future Smartcar integration. Not used by the fake-fuel first pass.
public sealed class SmartcarOptions
{
    public const string SectionName = "Smartcar";

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string? RefreshToken { get; set; }
}
