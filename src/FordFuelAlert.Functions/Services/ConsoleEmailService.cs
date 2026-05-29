using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;
using Microsoft.Extensions.Logging;

namespace FordFuelAlert.Functions.Services;

// First-pass email provider: logs the message instead of sending it.
// TODO: Add a real provider (SendGrid or Azure Communication Services) behind IEmailService,
//       selected via Email__Provider, using Email__ApiKey / Email__FromAddress from config or Key Vault.
public sealed class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[FAKE EMAIL] To={To} | Subject={Subject}{NewLine}{Body}",
            message.To, message.Subject, Environment.NewLine, message.Body);

        return Task.CompletedTask;
    }
}
