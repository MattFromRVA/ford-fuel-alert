namespace FordFuelAlert.Functions.Models;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public const string ConsoleProvider = "console";
    public const string AcsProvider = "acs";

    // "console" (logs instead of sending, default) or "acs" (Azure Communication Services).
    public string Provider { get; set; } = ConsoleProvider;

    // The "from" address. For ACS this must be a verified sender on your provisioned domain.
    public string? FromAddress { get; set; }

    // acs provider: connection string (endpoint + access key) ...
    public string? AcsConnectionString { get; set; }

    // ... or resource endpoint used with managed identity (preferred in Azure over the access key).
    public string? AcsEndpoint { get; set; }
}
