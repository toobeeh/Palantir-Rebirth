using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Palantir_Core.Patreon;

namespace Palantir_Core;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Palantir Core Service");
        
        var serviceProvider = CreateServices();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogDebug("Initialized service providers");

        var subs = await new PatreonApiClient("Yl0GOvXGugpx7R8N86Pc83rmPIP9HowA0NsAgOFGDTk", "7069838", "7385169").GetCurrentSubscriptions();
        
        await Task.Delay(-1);
    }

    static ServiceProvider CreateServices()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "Configuration"))
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true)
            .Build();
        
        var serviceProvider = new ServiceCollection()
            .AddLogging(builder => builder
                .AddConfiguration(configuration.GetSection("Logging"))
                .AddConsole())
            .BuildServiceProvider();

        return serviceProvider;
    }
}