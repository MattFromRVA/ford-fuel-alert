using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;

namespace FordFuelAlert.Tests.Fakes;

internal sealed class StubFuelService : IFuelService
{
    private readonly FuelStatus? _status;

    public StubFuelService(FuelStatus? status)
    {
        _status = status;
    }

    public Task<FuelStatus?> GetFuelStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_status);
    }
}

internal sealed class RecordingEmailService : IEmailService
{
    public List<EmailMessage> Sent { get; } = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryAlertStateService : IAlertStateService
{
    public InMemoryAlertStateService(AlertState? initial = null)
    {
        Current = initial;
    }

    public AlertState? Current { get; set; }

    public int SaveCount { get; private set; }

    public Task<AlertState?> GetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Current);
    }

    public Task SaveAsync(AlertState state, CancellationToken cancellationToken = default)
    {
        Current = state;
        SaveCount++;
        return Task.CompletedTask;
    }
}
