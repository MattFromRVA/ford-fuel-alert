using Azure;
using Azure.Communication.Email;
using FordFuelAlert.Functions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using AcsEmailMessage = Azure.Communication.Email.EmailMessage;
using EmailMessage = FordFuelAlert.Functions.Models.EmailMessage;

namespace FordFuelAlert.Tests;

public class AcsEmailServiceTests
{
    [Fact]
    public async Task Maps_recipient_subject_and_body_onto_the_acs_payload()
    {
        var client = new CapturingEmailClient();
        var service = new AcsEmailService(client, "alerts@fabrikam.azurecomm.net", NullLogger<AcsEmailService>.Instance);

        await service.SendAsync(new EmailMessage("owner@example.com", "Ford fuel is low", "Fuel: 20%"));

        Assert.NotNull(client.Captured);
        // Completed (not Started) so a failed send throws instead of being treated as delivered.
        Assert.Equal(WaitUntil.Completed, client.Wait);
        Assert.Equal("alerts@fabrikam.azurecomm.net", client.Captured!.SenderAddress);
        Assert.Equal("owner@example.com", Assert.Single(client.Captured.Recipients.To).Address);
        Assert.Equal("Ford fuel is low", client.Captured.Content.Subject);
        Assert.Equal("Fuel: 20%", client.Captured.Content.PlainText);
    }

    [Fact]
    public async Task Throws_without_sending_when_recipient_is_not_a_valid_address()
    {
        var client = new CapturingEmailClient();
        var service = new AcsEmailService(client, "alerts@fabrikam.azurecomm.net", NullLogger<AcsEmailService>.Instance);

        // The placeholder FuelAlertService uses when FuelAlert__RecipientEmail is unset.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SendAsync(new EmailMessage("(recipient not configured)", "Ford fuel is low", "Fuel: 20%")));

        Assert.Contains("FuelAlert__RecipientEmail", ex.Message);
        Assert.Null(client.Captured);
    }

    // Azure SDK clients expose a protected constructor and virtual methods for exactly this kind of test double.
    private sealed class CapturingEmailClient : EmailClient
    {
        public AcsEmailMessage? Captured { get; private set; }

        public WaitUntil? Wait { get; private set; }

        public override Task<EmailSendOperation> SendAsync(WaitUntil wait, AcsEmailMessage message, CancellationToken cancellationToken = default)
        {
            Captured = message;
            Wait = wait;
            // The service ignores the return value, so a null operation is fine here.
            return Task.FromResult<EmailSendOperation>(null!);
        }
    }
}
