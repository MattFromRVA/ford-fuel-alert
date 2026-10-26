using FordFuelAlert.Functions.Abstractions;
using FordFuelAlert.Functions.Models;
using FordFuelAlert.Functions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        services.Configure<FuelAlertOptions>(configuration.GetSection(FuelAlertOptions.SectionName));
        services.Configure<FuelOptions>(configuration.GetSection(FuelOptions.SectionName));
        services.Configure<FakeFuelOptions>(configuration.GetSection(FakeFuelOptions.SectionName));
        services.Configure<SmartcarOptions>(configuration.GetSection(SmartcarOptions.SectionName));
        services.Configure<FordConnectOptions>(configuration.GetSection(FordConnectOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<AlertStateOptions>(configuration.GetSection(AlertStateOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient();

        // Fuel data provider. Fuel__Provider selects "fake" (default), "smartcar" or "fordconnect".
        services.AddSingleton<IFuelService>(provider =>
        {
            var fuelOptions = configuration.GetSection(FuelOptions.SectionName).Get<FuelOptions>() ?? new FuelOptions();
            var alertOptions = configuration.GetSection(FuelAlertOptions.SectionName).Get<FuelAlertOptions>() ?? new FuelAlertOptions();
            var fakeOptions = configuration.GetSection(FakeFuelOptions.SectionName).Get<FakeFuelOptions>() ?? new FakeFuelOptions();
            var smartcarOptions = configuration.GetSection(SmartcarOptions.SectionName).Get<SmartcarOptions>() ?? new SmartcarOptions();
            var fordConnectOptions = configuration.GetSection(FordConnectOptions.SectionName).Get<FordConnectOptions>() ?? new FordConnectOptions();
            return FuelServiceFactory.Create(
                fuelOptions,
                alertOptions,
                fakeOptions,
                smartcarOptions,
                fordConnectOptions,
                new FuelServiceFactory.Dependencies(
                    provider.GetRequiredService<IHttpClientFactory>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<ILoggerFactory>()));
        });

        // Email provider. Email__Provider selects "console" (logs instead of sending, default) or
        // "acs" (Azure Communication Services). Invalid ACS config throws here on first resolution.
        services.AddSingleton<IEmailService>(provider =>
        {
            var emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return EmailServiceFactory.Create(emailOptions, loggerFactory);
        });

        // Alert state store. AlertState__Provider selects "file" (local JSON, default) or "table" (Azure Table Storage, durable).
        services.AddSingleton<IAlertStateService>(provider =>
        {
            var alertStateOptions = configuration.GetSection(AlertStateOptions.SectionName).Get<AlertStateOptions>() ?? new AlertStateOptions();
            var vehicleId = configuration[$"{FuelAlertOptions.SectionName}:VehicleId"];
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return AlertStateServiceFactory.Create(alertStateOptions, vehicleId, loggerFactory);
        });

        services.AddSingleton<FuelAlertService>();
    })
    .Build();

host.Run();
