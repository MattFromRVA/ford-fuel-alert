using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FordFuelAlert.Functions.Services;

// PLACEHOLDER — not implemented, and only registered when FuelAlert__UseFakeFuelService=false.
//
// TODO (future Smartcar integration — do NOT guess undocumented API details, follow the official docs):
//   1. Have the vehicle owner authorize access via Smartcar Connect (OAuth) and capture a refresh token.
//      Explicit owner authorization is required. Never store Ford account username/password.
//   2. Read Smartcar__ClientId / Smartcar__ClientSecret / Smartcar__RefreshToken from config or Key Vault.
//   3. Exchange the refresh token for an access token, then call the documented fuel endpoint.
//   4. Map the response onto FuelStatus (PercentRemaining 0-100, RangeMiles, RetrievedAtUtc, VehicleId).
public sealed class SmartcarFuelService : IFuelService
{
    private readonly SmartcarOptions _options;
    private readonly ILogger<SmartcarFuelService> _logger;

    public SmartcarFuelService(IOptions<SmartcarOptions> options, ILogger<SmartcarFuelService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<FuelStatus?> GetFuelStatusAsync(CancellationToken cancellationToken = default)
    {
        // Log only whether a credential is present, never its value.
        var hasRefreshToken = !string.IsNullOrWhiteSpace(_options.RefreshToken);
        _logger.LogWarning("SmartcarFuelService is not implemented. RefreshToken configured: {HasRefreshToken}.", hasRefreshToken);

        throw new NotImplementedException(
            "SmartcarFuelService is not implemented yet. Keep FuelAlert__UseFakeFuelService=true, or implement the "
            + "Smartcar integration described in this file's TODO before disabling the fake service.");
    }
}
