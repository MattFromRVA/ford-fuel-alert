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

// TimeProvider whose clock can be advanced by hand, so tests can drive the access-token cache expiry.
internal sealed class MutableTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public MutableTimeProvider(DateTimeOffset start)
    {
        _now = start;
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}

// Scripts HTTP responses per request so SmartcarFuelService can be tested without the network.
// The handler is given a function that inspects each outgoing request and returns the response
// (or throws, to simulate a transport failure). Every request is recorded for assertions.
internal sealed class ScriptedHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public ScriptedHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public List<HttpRequestMessage> Requests { get; } = new();

    // Request bodies captured at send time (before the request/content is disposed), indexed in
    // lockstep with Requests so tests can assert on the form payload after the call returns.
    public List<string?> RequestBodies { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
        return Task.FromResult(_responder(request));
    }

    // Body of the first request whose URL exactly matches, or null if none.
    public string? BodyFor(string url)
    {
        for (var i = 0; i < Requests.Count; i++)
        {
            if (Requests[i].RequestUri!.ToString() == url)
            {
                return RequestBodies[i];
            }
        }

        return null;
    }
}

// Minimal IHttpClientFactory that hands out clients over a single (usually scripted) handler.
internal sealed class SingleHandlerHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public SingleHandlerHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name)
    {
        // disposeHandler: false so the shared handler survives across CreateClient calls.
        return new HttpClient(_handler, disposeHandler: false);
    }
}
