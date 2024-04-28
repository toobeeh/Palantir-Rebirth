using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Palantir_Core.Discord;

public class DiscordApiClient(ILogger<DiscordApiClient> logger, IOptions<DiscordApiClientOptions> options)
{
    private readonly DiscordClient _client = new(new DiscordConfiguration
    {
        Token = options.Value.DiscordToken,
        TokenType = TokenType.Bot
    });

    public async Task<DiscordRoleMembers> GetRoleMembers()
    {
        logger.LogTrace("GetRoleMembers()");

        var guild = await _client.GetGuildAsync(options.Value.ServerId);
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

    public async Task SetStatus(int onlinePlayerCount, double dropRate)
    {
        var status = dropRate > 0 ? $"{onlinePlayerCount} ppl ({dropRate:0.#} boost)" : $"{onlinePlayerCount} people";
        await _client.UpdateStatusAsync(new DiscordActivity(status, DiscordActivityType.Watching));
    }
}