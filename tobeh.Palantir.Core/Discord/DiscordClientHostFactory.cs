using DSharpPlus;
using DSharpPlus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using tobeh.Palantir.Commands;

namespace tobeh.Palantir.Core.Discord;

public class DiscordClientHostFactory(
    ILogger<DiscordClientHostFactory> logger,
    IServiceProvider serviceProvider)
{
    /// <summary>
    /// Create a host which copies the existing services from the serviceProvider and adds a Discord client
    /// </summary>
    /// <param name="discordToken"></param>
    /// <param name="intents"></param>
    /// <returns></returns>
    public IHost CreateClientHost(string discordToken, DiscordIntents intents)
    {
        logger.LogTrace("CreateAndStartClientAsync(discordToken={discordToken}, intents={intents})", discordToken,
            intents);

        var builder = new HostBuilder()
            .ConfigureServices(services =>
            {
                // Register existing services from the serviceProvider
                foreach (var serviceDescriptor in serviceProvider.GetService<IEnumerable<ServiceDescriptor>>() ?? [])
                {
                    services.Add(serviceDescriptor);
                }

                services.AddDiscordClient(discordToken,
                        intents)
                    .AddSingleton<DiscordHostedBot>();
            })
            .Build();

        return builder;
    }
}