using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using tobeh.Palantir.Commands;
using tobeh.Palantir.Lobbies.Discord;
using tobeh.Palantir.Lobbies.Quartz.WorkerLobbyUpdater;
using tobeh.Palantir.Lobbies.Worker;
using tobeh.TypoImageGen.Client.Util;
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

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "Configuration"))
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            configBuilder.AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true);
        }

        var configuration = configBuilder.Build();

        builder.Services
            .AddTypoImageGeneratorGrpc(configuration.GetValue<string>("Grpc:ImageGenAddress"))
            .AddValmarGrpc(configuration.GetValue<string>("Grpc:ValmarAddress"))
            .Configure<DiscordOptions>(configuration.GetRequiredSection("Discord"))
            .AddQuartzHostedService()
            .AddQuartz(WorkerLobbyUpdaterConfiguration.Configure)
            .AddScoped<MemberContext>()
            .AddScoped<ServerHomeContext>()
            .AddSingleton<DiscordClientFactory>()
            .AddSingleton<WorkerState>()
            .AddLogging(builder => builder
                .AddConfiguration(configuration.GetSection("Logging"))
                .AddConsole())
            .BuildServiceProvider();

        return builder.Build();
    }
}