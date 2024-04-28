using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Palantir_Core.Discord;
using Palantir_Core.Patreon;
using Palantir_Core.Quartz.BubbleTracer;
using Palantir_Core.Quartz.BubbleUpdater;
using Palantir_Core.Quartz.DropScheduler;
using Palantir_Core.Quartz.FlagUpdater;
using Palantir_Core.Quartz.OnlineItemsUpdater;
using Palantir_Core.Quartz.VolatileDataClearer;
using Quartz;
using tobeh.Valmar.Client.Util;

namespace Palantir_Core;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Palantir Core Service");

        // register services
        var host = CreateHost();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogDebug("Initialized service providers");

        await host.RunAsync();
        await Task.Delay(-1);
    }

    static IHost CreateHost()
    {
        var host = Host.CreateApplicationBuilder();
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "Configuration"))
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            configBuilder.AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true);
        }

        var configuration = configBuilder.Build();

        host.Services
            .AddValmarGrpc(configuration.GetValue<string>("Grpc:Address"))
            .AddSingleton<PatreonApiClient>()
            .Configure<PatreonApiClientOptions>(configuration.GetRequiredSection("Patreon"))
            .AddSingleton<DiscordApiClient>()
            .AddHostedService<DiscordApiClient>(p => p.GetRequiredService<DiscordApiClient>())
            .Configure<DiscordApiClientOptions>(configuration.GetRequiredSection("Discord"))
            .AddLogging(builder => builder
                .AddConfiguration(configuration.GetSection("Logging"))
                .AddConsole())
            .AddQuartz(OnlineItemsUpdaterConfiguration.Configure)
            .AddQuartz(FlagUpdaterConfiguration.Configure)
            .AddQuartz(BubbleTracerConfiguration.Configure)
            .AddQuartz(BubbleUpdaterConfiguration.Configure)
            .AddQuartz(VolatileDataClearerConfiguration.Configure)
            .AddQuartz(DropSchedulerConfiguration.Configure)
            .AddQuartzHostedService(options => { options.WaitForJobsToComplete = true; })
            .BuildServiceProvider();

        return host.Build();
    }
}