using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace tobeh.Palantir.Core.Discord;

public abstract class DiscordApiClient(
    ILogger<DiscordApiClient> logger) : IHostedService
{
    protected abstract IHost ClientHost { get; }

    /// <summary>
    /// Start the hosted bot, but not the host itself to avoid duplicating inherited hosted services
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("StartAsync()");
        await ClientHost.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Stop the hosted bot and dispose its enclosing host
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("StopAsync()");
        await ClientHost.StartAsync(cancellationToken);
        ClientHost.Dispose();
    }
}

public class PalantirApiClient(
    ILogger<PalantirApiClient> logger,
    DiscordClientHostFactory clientHostFactory,
    IOptions<PalantirApiClientOptions> options) : DiscordApiClient(logger)
{
    protected override IHost ClientHost { get; } = clientHostFactory
        .CreateClientHost(options.Value.DiscordToken, DiscordIntents.None);

    public async Task SetStatus(int onlinePlayerCount, double dropRate)
    {
        var status = dropRate > 1
            ? $"{onlinePlayerCount} ppl ({dropRate:0.#} boost)"
            : $"{onlinePlayerCount} ppl on skribbl.io";
        await ClientHost.Services.GetRequiredService<DiscordClient>()
            .UpdateStatusAsync(new DiscordActivity(status, DiscordActivityType.Watching));
    }
}

public class ServantApiClient(
    ILogger<ServantApiClient> logger,
    DiscordClientHostFactory clientHostFactory,
    IOptions<ServantApiClientOptions> options) : DiscordApiClient(logger)
{
    protected override IHost ClientHost { get; } = clientHostFactory
        .CreateClientHost(options.Value.DiscordToken, DiscordIntents.GuildMembers | DiscordIntents.Guilds);

    public async Task<DiscordRoleMembers> GetRoleMembers()
    {
        logger.LogTrace("GetRoleMembers()");

        var guild = await ClientHost.Services.GetRequiredService<DiscordClient>()
            .GetGuildAsync(options.Value.ServerId);
        var betaMembers = new List<long>();
        var boostMembers = new List<long>();

        await foreach (var member in guild.GetAllMembersAsync())
        {
            var roles = member.Roles.Select(r => r.Id).ToList();
            if (roles.Count == 0)
                continue;

            if (roles.Any(role => role == options.Value.BetaRoleId))
                betaMembers.Add(Convert.ToInt64(member.Id));
            if (roles.Any(role => role == options.Value.BoostRoleId))
                boostMembers.Add(Convert.ToInt64(member.Id));
        }

        return new DiscordRoleMembers(betaMembers, boostMembers);
    }
}