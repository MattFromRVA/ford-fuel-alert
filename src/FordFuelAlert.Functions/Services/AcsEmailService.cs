using Azure;
using Azure.Communication.Email;
using FordFuelAlert.Functions.Abstractions;
using Microsoft.Extensions.Logging;
// Both our model and the ACS SDK define an EmailMessage; the alias makes the unqualified name our model.
using EmailMessage = FordFuelAlert.Functions.Models.EmailMessage;

namespace FordFuelAlert.Functions.Services;

// Real email provider backed by Azure Communication Services, selected via Email__Provider=acs.
// The EmailClient is constructed in EmailServiceFactory (from a connection string or endpoint + managed identity)
// so this class stays focused on mapping our EmailMessage onto the ACS payload and can be unit-tested with a faked client.
public sealed class AcsEmailService : IEmailService
{
    private readonly EmailClient _client;
    private readonly string _fromAddress;
    private readonly ILogger<AcsEmailService> _logger;

    public AcsEmailService(EmailClient client, string fromAddress, ILogger<AcsEmailService> logger)
    {
        _client = client;
        _fromAddress = fromAddress;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var content = new EmailContent(message.Subject) { PlainText = message.Body };
        var recipients = new EmailRecipients(new[] { new EmailAddress(message.To) });
        var acsMessage = new Azure.Communication.Email.EmailMessage(_fromAddress, recipients, content);

        // WaitUntil.Started: queue the message and return; a failure to queue throws RequestFailedException.
        await _client.SendAsync(WaitUntil.Started, acsMessage, cancellationToken);

        _logger.LogInformation("Alert email queued via Azure Communication Services to {Recipient}.", message.To);
    }
}
