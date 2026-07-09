namespace FordFuelAlert.Functions.Abstractions;

// Durable store for the Smartcar refresh token. Smartcar rotates the refresh token on every
// exchange, so the newly issued token must be persisted or the integration is stranded.
public interface ISmartcarTokenStore
{
    // Returns the persisted refresh token, or null if none has been stored yet (first run).
    Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default);

    Task SaveRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}
