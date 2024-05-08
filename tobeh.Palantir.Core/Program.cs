using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using tobeh.Palantir.Core.Discord;
using tobeh.Palantir.Core.Patreon;
using tobeh.Palantir.Core.Quartz.BubbleTracer;
using tobeh.Palantir.Core.Quartz.BubbleUpdater;
using tobeh.Palantir.Core.Quartz.DropScheduler;
using tobeh.Palantir.Core.Quartz.FlagUpdater;
using tobeh.Palantir.Core.Quartz.OnlineItemsUpdater;
using tobeh.Palantir.Core.Quartz.VolatileDataClearer;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Core;

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