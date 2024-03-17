using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Palantir_Commands.Discord;
using Valmar_Client.Grpc;

namespace Palantir_Commands;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Palantir Commands Service");
        
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
            .AddGrpcClients(Assembly.Load("Valmar-Client"), configuration.GetValue<string>("Grpc:Address"))
            .AddLogging(builder => builder
                .AddConfiguration(configuration.GetSection("Logging"))
                .AddConsole())
            .Configure<DiscordBotClientOptions>(configuration.GetRequiredSection("Discord"))
            .AddHostedService<DiscordBotClient>()
            .BuildServiceProvider();

        return builder.Build();
    }
}