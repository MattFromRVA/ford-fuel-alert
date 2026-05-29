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

        services.AddSingleton(TimeProvider.System);

        // Fuel data provider. Default = fake. Set FuelAlert__UseFakeFuelService=false to route to Smartcar (not yet implemented).
        var useFakeFuelService = configuration.GetValue($"{FuelAlertOptions.SectionName}:UseFakeFuelService", true);
        if (useFakeFuelService)
        {
            services.AddSingleton<IFuelService, FakeFuelService>();
        }
        else
        {
            services.AddSingleton<IFuelService, SmartcarFuelService>();
        }

        // Email provider. Default = console (logs instead of sending).
        // TODO: Add SendGrid / Azure Communication Services and select via Email__Provider.
        services.AddSingleton<IEmailService, ConsoleEmailService>();

        // Alert state store. Default = local JSON file for development.
        // TODO: Swap to TableAlertStateService (or another durable store) when running in Azure.
        services.AddSingleton<IAlertStateService>(provider =>
        {
            var stateFilePath = configuration["AlertState:FilePath"];
            if (string.IsNullOrWhiteSpace(stateFilePath))
            {
                stateFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ford-fuel-alert",
                    "alert-state.json");
            }

            var logger = provider.GetRequiredService<ILogger<FileAlertStateService>>();
            return new FileAlertStateService(stateFilePath, logger);
        });

        services.AddSingleton<FuelAlertService>();
    })
    .Build();

host.Run();
