namespace FordFuelAlert.Functions.Models;

// Configuration for the real Smartcar fuel provider (used when FuelAlert__UseFakeFuelService=false).
// Targets Smartcar Vehicle API v3: an application-level token from the client credentials grant,
// plus the vehicle owner's Smartcar user id sent as the sc-user-id header on every vehicle request.
public sealed class SmartcarOptions
{
    public const string SectionName = "Smartcar";

    // API credentials from the Smartcar Dashboard (API Credentials tab).
    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    // Smartcar user id (UUID) returned as user_id on the Connect redirect after the vehicle owner
    // authorizes access. Sent as sc-user-id on every vehicle request.
    public string? UserId { get; set; }

    // Smartcar vehicle id — the {vehicleId} in GET /vehicles/{vehicleId}/signals/{code}. Optional:
    // when empty, the provider looks it up from the user's single connection (GET /connections).
    public string? VehicleId { get; set; }
}
