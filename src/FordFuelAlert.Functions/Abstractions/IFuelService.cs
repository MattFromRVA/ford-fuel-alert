using FordFuelAlert.Functions.Models;

namespace FordFuelAlert.Functions.Abstractions;

// Supplies the current fuel status for the configured vehicle. Swap the implementation to change data provider.
public interface IFuelService
{
    Task<FuelStatus?> GetFuelStatusAsync(CancellationToken cancellationToken = default);
}
