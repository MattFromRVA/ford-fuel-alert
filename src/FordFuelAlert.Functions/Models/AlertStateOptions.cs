namespace FordFuelAlert.Functions.Models;

public sealed class AlertStateOptions
{
    public const string SectionName = "AlertState";

    public const string FileProvider = "file";
    public const string TableProvider = "table";

    // "file" (local development, default) or "table" (Azure Table Storage, durable).
    public string Provider { get; set; } = FileProvider;

    // file provider: where the JSON state lives. Empty = default under local app-data.
    public string? FilePath { get; set; }

    // table provider: connection string (local Azurite / access key) ...
    public string? ConnectionString { get; set; }

    // ... or table service endpoint used with managed identity (preferred in Azure).
    public string? ServiceUri { get; set; }

    public string TableName { get; set; } = "FuelAlertState";
}
