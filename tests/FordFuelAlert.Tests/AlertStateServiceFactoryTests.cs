using FordFuelAlert.Functions.Models;
using FordFuelAlert.Functions.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FordFuelAlert.Tests;

public class AlertStateServiceFactoryTests
{
    private static readonly string TempFilePath = Path.Combine(Path.GetTempPath(), "ford-fuel-alert-tests", "state.json");

    [Fact]
    public void Create_DefaultsToFileService()
    {
        var options = new AlertStateOptions { FilePath = TempFilePath };

        var service = AlertStateServiceFactory.Create(options, "my-ford", NullLoggerFactory.Instance);

        Assert.IsType<FileAlertStateService>(service);
    }

    [Fact]
    public void Create_ReturnsTableService_WhenConnectionStringConfigured()
    {
        var options = new AlertStateOptions
        {
            Provider = AlertStateOptions.TableProvider,
            ConnectionString = "UseDevelopmentStorage=true",
        };

        var service = AlertStateServiceFactory.Create(options, "my-ford", NullLoggerFactory.Instance);

        Assert.IsType<TableAlertStateService>(service);
    }

    [Fact]
    public void Create_ReturnsTableService_WhenServiceUriConfigured()
    {
        var options = new AlertStateOptions
        {
            Provider = AlertStateOptions.TableProvider,
            ServiceUri = "https://example.table.core.windows.net",
        };

        var service = AlertStateServiceFactory.Create(options, "my-ford", NullLoggerFactory.Instance);

        Assert.IsType<TableAlertStateService>(service);
    }

    [Fact]
    public void Create_IgnoresProviderCasing()
    {
        var options = new AlertStateOptions
        {
            Provider = "Table",
            ConnectionString = "UseDevelopmentStorage=true",
        };

        var service = AlertStateServiceFactory.Create(options, "my-ford", NullLoggerFactory.Instance);

        Assert.IsType<TableAlertStateService>(service);
    }

    [Fact]
    public void Create_Throws_WhenTableProviderHasNoConnectionInfo()
    {
        var options = new AlertStateOptions { Provider = AlertStateOptions.TableProvider };

        var ex = Assert.Throws<InvalidOperationException>(
            () => AlertStateServiceFactory.Create(options, "my-ford", NullLoggerFactory.Instance));

        Assert.Contains("AlertState__ConnectionString", ex.Message);
        Assert.Contains("AlertState__ServiceUri", ex.Message);
    }

    [Fact]
    public void Create_Throws_OnUnknownProvider()
    {
        var options = new AlertStateOptions { Provider = "cosmos" };

        var ex = Assert.Throws<InvalidOperationException>(
            () => AlertStateServiceFactory.Create(options, "my-ford", NullLoggerFactory.Instance));

        Assert.Contains("cosmos", ex.Message);
    }
}
