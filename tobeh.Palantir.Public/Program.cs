using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using tobeh.Palantir.Commands;
using tobeh.Palantir.Public.Discord;
using tobeh.TypoContentService.Client.Util;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Public;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Palantir Public Service");
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
            .AddLogging(loggingBuilder => loggingBuilder
                .AddConfiguration(builder.Configuration.GetSection("Logging"))
                .AddConsole())
            .Configure<DiscordBotClientOptions>(builder.Configuration.GetRequiredSection("Discord"))
            .AddHostedService<DiscordBotClient>()
            .AddScoped<MemberContext>()
            .AddScoped<ServerHomeContext>()
            .BuildServiceProvider();

        return builder.Build();
    }
}