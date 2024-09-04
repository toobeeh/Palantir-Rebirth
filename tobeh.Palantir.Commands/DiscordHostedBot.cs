using DSharpPlus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace tobeh.Palantir.Commands;

public class DiscordHostedBot(DiscordClient discordClient, ILogger<DiscordHostedBot> logger) : IHostedService
{
    public DiscordClient DiscordClient => discordClient;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("StartAsync()");

        await discordClient.ConnectAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("StopAsync()");

        await discordClient.DisconnectAsync();
    }
}