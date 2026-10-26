namespace FordFuelAlert.Functions.Models;

public sealed class FordConnectOptions
{
    public const string SectionName = "FordConnect";

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string? RefreshToken { get; set; }

    public string? Vin { get; set; }

    public double MaxDataAgeHours { get; set; } = 48;

    public string? TokenStoreConnectionString { get; set; }

    public string? TokenStoreServiceUri { get; set; }

    public string TokenStoreTableName { get; set; } = "FordConnectTokens";
}
