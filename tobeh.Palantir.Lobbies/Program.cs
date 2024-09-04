using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using tobeh.Palantir.Commands;
using tobeh.Palantir.Lobbies.Discord;
using tobeh.Palantir.Lobbies.Quartz.DiscordLobbyUpdater;
using tobeh.Palantir.Lobbies.Quartz.LobbyLinksUpdater;
using tobeh.Palantir.Lobbies.Worker;
using tobeh.TypoContentService.Client.Util;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Lobbies;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Palantir Lobbies Service");
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        // create host and run
        var host = CreateHost(args);
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogDebug("Initialized service providers");

        await host.RunAsync();
    }

    static IHost CreateHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services
            .AddTypoContentServiceGrpc(builder.Configuration.GetValue<string>("Grpc:ContentServiceAddress"))
            .AddValmarGrpc(builder.Configuration.GetValue<string>("Grpc:ValmarAddress"))
            .Configure<DiscordOptions>(builder.Configuration.GetRequiredSection("Discord"))
            .Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"))
            .AddSingleton<WorkerState>()
            .AddScoped<WorkerService>()
            .AddQuartzHostedService()
            .AddQuartz(DiscordLobbyUpdaterConfiguration.Configure)
            .AddQuartz(LobbyLinksUpdaterConfiguration.Configure)
            .AddScoped<MemberContext>()
            .AddScoped<ServerHomeContext>()
            .AddSingleton<DiscordBotHostFactory>()
            .AddLogging(loggingBuilder => loggingBuilder
                .AddConfiguration(builder.Configuration.GetSection("Logging"))
                .AddConsole())
            .BuildServiceProvider();

        return builder.Build();
    }
}