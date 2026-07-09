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
        // A real provider can't send to nobody. The console provider tolerates a missing recipient
        // (logs a placeholder), but here that would reach ACS as an invalid address on every low-fuel
        // run, so fail with a clear, actionable error instead of a cryptic ACS validation failure.
        if (string.IsNullOrWhiteSpace(message.To) || !message.To.Contains('@'))
        {
            throw new InvalidOperationException(
                $"Cannot send an alert email via Azure Communication Services without a valid recipient address "
                + $"(got '{message.To}'). Set FuelAlert__RecipientEmail.");
        }

        var content = new EmailContent(message.Subject) { PlainText = message.Body };
        var recipients = new EmailRecipients(new[] { new EmailAddress(message.To) });
        var acsMessage = new Azure.Communication.Email.EmailMessage(_fromAddress, recipients, content);

        // WaitUntil.Completed: poll the send operation to a terminal state so a failed send throws
        // (RequestFailedException) rather than returning "success". FuelAlertService only marks
        // AlertSent=true after this returns, so throwing on failure keeps the alert armed for a retry.
        await _client.SendAsync(WaitUntil.Completed, acsMessage, cancellationToken);

        _logger.LogInformation("Alert email sent via Azure Communication Services to {Recipient}.", message.To);
    }
}
