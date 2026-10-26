namespace FordFuelAlert.Functions.Models;

public sealed class FordConnectToken
{
    public required string RefreshToken { get; init; }

    public DateTimeOffset? AuthTimeUtc { get; init; }

    public DateTimeOffset? LastRefreshedUtc { get; init; }
}
