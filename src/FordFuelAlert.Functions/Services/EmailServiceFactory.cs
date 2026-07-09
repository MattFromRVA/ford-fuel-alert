using Azure.Communication.Email;
using Azure.Identity;
using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;
using Microsoft.Extensions.Logging;

namespace FordFuelAlert.Functions.Services;

// Builds the IEmailService selected by Email__Provider ("console" or "acs").
// Invalid ACS configuration throws here (at first resolution), so startup fails fast with a clear message.
public static class EmailServiceFactory
{
    public static IEmailService Create(EmailOptions options, ILoggerFactory loggerFactory)
    {
        if (string.Equals(options.Provider, EmailOptions.ConsoleProvider, StringComparison.OrdinalIgnoreCase))
        {
            return new ConsoleEmailService(loggerFactory.CreateLogger<ConsoleEmailService>());
        }

        if (string.Equals(options.Provider, EmailOptions.AcsProvider, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.FromAddress))
            {
                throw new InvalidOperationException(
                    "Email__Provider is 'acs' but Email__FromAddress is not set. "
                    + "Set it to a verified sender address on your Azure Communication Services domain.");
            }

            return new AcsEmailService(
                CreateEmailClient(options),
                options.FromAddress,
                loggerFactory.CreateLogger<AcsEmailService>());
        }

        throw new InvalidOperationException(
            $"Unknown email provider '{options.Provider}'. "
            + $"Set Email__Provider to '{EmailOptions.ConsoleProvider}' or '{EmailOptions.AcsProvider}'.");
    }

    private static EmailClient CreateEmailClient(EmailOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.AcsConnectionString))
        {
            return new EmailClient(options.AcsConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(options.AcsEndpoint))
        {
            // Managed identity (or developer credentials locally) — preferred in Azure over the access key.
            return new EmailClient(new Uri(options.AcsEndpoint), new DefaultAzureCredential());
        }

        throw new InvalidOperationException(
            "Email__Provider is 'acs' but neither Email__AcsConnectionString nor Email__AcsEndpoint is set.");
    }
}
