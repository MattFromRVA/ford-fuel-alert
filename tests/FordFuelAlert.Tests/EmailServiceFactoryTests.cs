using FordFuelAlert.Functions.Models;
using FordFuelAlert.Functions.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FordFuelAlert.Tests;

public class EmailServiceFactoryTests
{
    [Fact]
    public void Defaults_to_console_provider()
    {
        var service = EmailServiceFactory.Create(new EmailOptions(), NullLoggerFactory.Instance);

        Assert.IsType<ConsoleEmailService>(service);
    }

    [Fact]
    public void Builds_acs_provider_when_fully_configured()
    {
        var options = new EmailOptions
        {
            Provider = EmailOptions.AcsProvider,
            FromAddress = "alerts@fabrikam.azurecomm.net",
            AcsConnectionString = "endpoint=https://example.communication.azure.com/;accesskey=Kg==",
        };

        var service = EmailServiceFactory.Create(options, NullLoggerFactory.Instance);

        Assert.IsType<AcsEmailService>(service);
    }

    [Fact]
    public void Throws_when_acs_from_address_missing()
    {
        var options = new EmailOptions
        {
            Provider = EmailOptions.AcsProvider,
            AcsConnectionString = "endpoint=https://example.communication.azure.com/;accesskey=Kg==",
        };

        var ex = Assert.Throws<InvalidOperationException>(() => EmailServiceFactory.Create(options, NullLoggerFactory.Instance));
        Assert.Contains("Email__FromAddress", ex.Message);
    }

    [Fact]
    public void Throws_when_acs_has_no_connection_string_or_endpoint()
    {
        var options = new EmailOptions
        {
            Provider = EmailOptions.AcsProvider,
            FromAddress = "alerts@fabrikam.azurecomm.net",
        };

        var ex = Assert.Throws<InvalidOperationException>(() => EmailServiceFactory.Create(options, NullLoggerFactory.Instance));
        Assert.Contains("Email__AcsConnectionString", ex.Message);
    }

    [Fact]
    public void Throws_on_unknown_provider()
    {
        var options = new EmailOptions { Provider = "smtp" };

        var ex = Assert.Throws<InvalidOperationException>(() => EmailServiceFactory.Create(options, NullLoggerFactory.Instance));
        Assert.Contains("Unknown email provider", ex.Message);
    }
}
