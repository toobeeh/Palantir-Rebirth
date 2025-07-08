using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using Quartz;
using tobeh.Palantir.Core.Discord;
using tobeh.Palantir.Core.Patreon;
using tobeh.Palantir.Core.Quartz.BubbleTracer;
using tobeh.Palantir.Core.Quartz.BubbleUpdater;
using tobeh.Palantir.Core.Quartz.DropScheduler;
using tobeh.Palantir.Core.Quartz.FlagUpdater;
using tobeh.Palantir.Core.Quartz.OnlineItemsUpdater;
using tobeh.Palantir.Core.Quartz.RoleUpdater;
using tobeh.Palantir.Core.Quartz.VolatileDataClearer;
using tobeh.TypoLinkedRolesService.Client.Util;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Core;

public class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Palantir Core Service");

        // register services
        var host = CreateHost(args);
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogDebug("Initialized service providers");

        var metricServer = new KestrelMetricServer(port: 9090);
        metricServer.Start();

        await host.RunAsync();
    }

    private static IHost CreateHost(string[] args)
    {
        var host = Host.CreateApplicationBuilder(args);

        host.Services
            .AddValmarGrpc(host.Configuration.GetValue<string>("Grpc:ValmarAddress"))
            .AddTypoLinkedRolesServiceGrpc(host.Configuration.GetValue<string>("Grpc:LinkedRolesAddress"))
            .AddSingleton<PatreonApiClient>()
            .Configure<PatreonApiClientOptions>(host.Configuration.GetRequiredSection("Patreon"))
            .AddSingleton<ServantApiClient>()
            .AddSingleton<PalantirApiClient>()
            .AddSingleton<DiscordClientHostFactory>()
            .AddScoped<MemberRoleUpdateCollector>()
            .AddHostedService<PalantirApiClient>(p => p.GetRequiredService<PalantirApiClient>())
            .AddHostedService<ServantApiClient>(p => p.GetRequiredService<ServantApiClient>())
            .Configure<PalantirApiClientOptions>(host.Configuration.GetRequiredSection("Palantir"))
            .Configure<ServantApiClientOptions>(host.Configuration.GetRequiredSection("Servant"))
            .AddLogging(builder => builder
                .AddConfiguration(host.Configuration.GetSection("Logging"))
                .AddConsole())
            .AddQuartz(OnlineItemsUpdaterConfiguration.Configure)
            .AddQuartz(FlagUpdaterConfiguration.Configure)
            .AddQuartz(BubbleTracerConfiguration.Configure)
            .AddQuartz(BubbleUpdaterConfiguration.Configure)
            .AddQuartz(VolatileDataClearerConfiguration.Configure)
            .AddQuartz(DropSchedulerConfiguration.Configure)
            .AddQuartz(RoleUpdaterConfiguration.Configure)
            .AddQuartzHostedService(options => { options.WaitForJobsToComplete = true; })
            .BuildServiceProvider();

        return host.Build();
    }
}