﻿using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
using Valmar_Client.Grpc;

namespace Palantir_Core;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Palantir Core Service");
        
        // register services
        var serviceProvider = CreateServices();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogDebug("Initialized service providers");
        
        // start scheduled jobs via DI
        var schedulerFactory = serviceProvider.GetRequiredService<ISchedulerFactory>();
        var scheduler = await schedulerFactory.GetScheduler();
        await scheduler.Start();
        
        await Task.Delay(-1);
    }

    static ServiceProvider CreateServices()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "Configuration"))
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            builder.AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true);
        }
        var configuration = builder.Build();
        
        return new ServiceCollection()
            .AddGrpcClients(Assembly.Load("Valmar-Client"), configuration.GetValue<string>("Grpc:Address"))
            .AddSingleton<PatreonApiClient>()
            .Configure<PatreonApiClientOptions>(configuration.GetRequiredSection("Patreon"))
            .AddSingleton<DiscordApiClient>()
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
            .BuildServiceProvider();
    }
}