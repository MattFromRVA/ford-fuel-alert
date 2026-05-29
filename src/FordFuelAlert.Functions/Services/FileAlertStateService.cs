using System.Text.Json;
using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;
using Microsoft.Extensions.Logging;

namespace FordFuelAlert.Functions.Services;

// First-pass state store: persists AlertState as JSON on disk. Suitable for local development only.
// TODO: The Azure Functions file system is ephemeral, so use a durable store in Azure (see TableAlertStateService).
public sealed class FileAlertStateService : IAlertStateService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly ILogger<FileAlertStateService> _logger;

    public FileAlertStateService(string filePath, ILogger<FileAlertStateService> logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public async Task<AlertState?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogInformation("No existing alert state at {Path}; starting fresh.", _filePath);
            return null;
        }

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<AlertState>(stream, SerializerOptions, cancellationToken);
    }

    public async Task SaveAsync(AlertState state, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
        _logger.LogInformation("Saved alert state to {Path} (AlertSent={AlertSent}).", _filePath, state.AlertSent);
    }
}
