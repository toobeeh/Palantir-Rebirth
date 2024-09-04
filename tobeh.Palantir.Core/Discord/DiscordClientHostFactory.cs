using DSharpPlus;
using DSharpPlus.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using tobeh.Palantir.Commands;

namespace tobeh.Palantir.Core.Discord;

public class DiscordClientHostFactory(
    ILogger<DiscordClientHostFactory> logger,
    IConfiguration configuration)
{
    /// <summary>
    /// Create a host which contains a Discord client
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
                services.AddDiscordClient(discordToken,
                        intents)
                    .AddHostedService<DiscordHostedBot>()
                    .AddLogging(loggingBuilder => loggingBuilder
                        .AddConfiguration(configuration.GetSection("Logging"))
                        .AddConsole());
            })
            .Build();

        return builder;
    }
}