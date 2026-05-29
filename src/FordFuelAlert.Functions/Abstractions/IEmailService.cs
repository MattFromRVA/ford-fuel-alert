using FordFuelAlert.Functions.Models;

namespace FordFuelAlert.Functions.Abstractions;

// Sends notification emails. Swap the implementation to change email provider.
public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
