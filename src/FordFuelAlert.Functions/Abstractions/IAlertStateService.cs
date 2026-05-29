using FordFuelAlert.Functions.Models;

namespace FordFuelAlert.Functions.Abstractions;

// Persists low-fuel alert state between runs. Swap the implementation to change storage backend.
public interface IAlertStateService
{
    Task<AlertState?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AlertState state, CancellationToken cancellationToken = default);
}
