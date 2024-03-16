using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Valmar_Client.Grpc;

namespace Palantir_Commands;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Palantir Commands Service");
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
            .AddGrpcClients(Assembly.GetExecutingAssembly(), configuration.GetValue<string>("Grpc:Address"))
            .AddLogging(builder => builder
                .AddConfiguration(configuration.GetSection("Logging"))
                .AddConsole())
            .BuildServiceProvider();
    }
}