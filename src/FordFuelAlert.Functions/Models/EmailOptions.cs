namespace FordFuelAlert.Functions.Models;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? Provider { get; set; }

    public string? ApiKey { get; set; }

    public string? FromAddress { get; set; }
}
