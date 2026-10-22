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
        services.Configure<FakeFuelOptions>(configuration.GetSection(FakeFuelOptions.SectionName));
        services.Configure<SmartcarOptions>(configuration.GetSection(SmartcarOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<AlertStateOptions>(configuration.GetSection(AlertStateOptions.SectionName));

        services.AddSingleton(TimeProvider.System);

        // Fuel data provider. Default = fake. Set FuelAlert__UseFakeFuelService=false to route to the real Smartcar provider.
        var useFakeFuelService = configuration.GetValue($"{FuelAlertOptions.SectionName}:UseFakeFuelService", true);
        if (useFakeFuelService)
        {
            services.AddSingleton<IFuelService, FakeFuelService>();
        }
        else
        {
            services.AddHttpClient();
            services.AddSingleton<IFuelService, SmartcarFuelService>();
        }

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
