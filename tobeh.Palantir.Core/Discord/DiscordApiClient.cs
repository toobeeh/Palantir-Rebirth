using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace tobeh.Palantir.Core.Discord;

public class DiscordApiClient(
    ILogger<DiscordApiClient> logger,
    IOptions<DiscordApiClientOptions> options,
    ILoggerFactory loggerFactory) : IHostedService
{
    protected readonly DiscordClient Client = new(new DiscordConfiguration
    {
        Token = options.Value.DiscordToken,
        TokenType = TokenType.Bot,
        LoggerFactory = loggerFactory
    });

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("StartAsync()");
        await Client.ConnectAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("StopAsync()");
        await Client.DisconnectAsync();
    }
}

public class PalantirApiClient(
    ILogger<PalantirApiClient> logger,
    IOptions<PalantirApiClientOptions> options,
    ILoggerFactory loggerFactory) : DiscordApiClient(logger, options, loggerFactory)
{
    public async Task SetStatus(int onlinePlayerCount, double dropRate)
    {
        var status = dropRate > 1
            ? $"{onlinePlayerCount} ppl ({dropRate:0.#} boost)"
            : $"{onlinePlayerCount} ppl on skribbl.io";
        await Client.UpdateStatusAsync(new DiscordActivity(status, DiscordActivityType.Watching));
    }
}

public class ServantApiClient(
    ILogger<ServantApiClient> logger,
    IOptions<ServantApiClientOptions> options,
    ILoggerFactory loggerFactory) : DiscordApiClient(logger, options, loggerFactory)
{
    public async Task<DiscordRoleMembers> GetRoleMembers()
    {
        logger.LogTrace("GetRoleMembers()");

        var guild = await Client.GetGuildAsync(options.Value.ServerId);
        var betaMembers = new List<long>();
        var boostMembers = new List<long>();

        await foreach (var member in guild.GetAllMembersAsync())
        {
            if (member.Roles.Any(role => role.Id == options.Value.BetaRoleId))
                betaMembers.Add(Convert.ToInt64(member.Id));
            if (member.Roles.Any(role => role.Id == options.Value.BoostRoleId))
                boostMembers.Add(Convert.ToInt64(member.Id));
        }

        return new DiscordRoleMembers(betaMembers, boostMembers);
    }
}