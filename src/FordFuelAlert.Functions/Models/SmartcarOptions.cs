namespace FordFuelAlert.Functions.Models;

// Configuration for the real Smartcar fuel provider (used when FuelAlert__UseFakeFuelService=false).
public sealed class SmartcarOptions
{
    public const string SectionName = "Smartcar";

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    // Bootstrap refresh token from the one-time OAuth onboarding. After the first run the rotated
    // token is persisted in the token store (Table Storage); this value only seeds an empty store.
    public string? RefreshToken { get; set; }

    // Smartcar vehicle id (UUID from the Connect flow) — the {id} in GET /vehicles/{id}/fuel.
    public string? VehicleId { get; set; }

    // Token store (Azure Table Storage): connection string (local Azurite / access key) ...
    public string? ConnectionString { get; set; }

    // ... or table service endpoint used with managed identity (preferred in Azure).
    public string? ServiceUri { get; set; }

    public string TableName { get; set; } = "SmartcarTokens";
}
