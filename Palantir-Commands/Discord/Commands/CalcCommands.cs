using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Palantir_Commands.Discord.Checks;
using Palantir_Commands.Discord.Extensions;
using Palantir_Commands.Services;
using tobeh.Valmar;

namespace Palantir_Commands.Discord.Commands;

/// <summary>
/// Calculate bubble goals and leaderboard positions
/// </summary>
/// <param name="logger"></param>
/// <param name="memberContext"></param>
/// <param name="guildsClient"></param>
/// <param name="statsClient"></param>
[Command("calculate"), TextAlias("calc")]
public class CalcCommands(
    ILogger<CalcCommands> logger,
    MemberContext memberContext,
    Guilds.GuildsClient guildsClient,
    Stats.StatsClient statsClient)
{
    /// <summary>
    /// Calculate the time needed to reach a certain amount of bubbles
    /// </summary>
    /// <param name="context"></param>
    /// <param name="amount">The amount of bubbles that should be collected</param>
    [Command("bubbles"), TextAlias("b")]
    public async Task CalculateBubbles(CommandContext context, uint amount)
    {
        logger.LogTrace("CalculateBubbles(amount={amount})", amount);

        var seconds = amount * 10;
        var time = TimeSpan.FromSeconds(seconds);

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"Time to reach {amount} bubbles")
            .WithDescription(
                $"To collect {amount} more bubbles, you have to spend `{time.AsCountdownTimespan()}` on skribbl.");

        await context.RespondAsync(embed);
    }

    /// <summary>
    /// Calculate the time needed to reach a certain position on the leaderboard
    /// </summary>
    /// <param name="context"></param>
    /// <param name="position">The position in the leaderboard that should be reached</param>
    [Command("rank"), TextAlias("rk"), RequirePalantirMember, RequireGuild]
    public async Task CalculateLeaderboard(CommandContext context, uint position)
    {
        logger.LogTrace("CalculateLeaderboard(position={position})", position);

        var guild = await guildsClient.GetGuildByDiscordIdAsync(new GetGuildByIdMessage
            { DiscordId = (long)context.Guild!.Id });
        var leaderboard = await statsClient.GetLeaderboardAsync(new GetLeaderboardMessage
        {
            GuildToken = guild.ObserveToken,
            Mode = LeaderboardMode.Bubbles
        });
        var member = memberContext.Member;

        var dict = leaderboard.Entries.ToDictionary(pos => pos.Rank);
        dict.TryGetValue((int)position, out var rank);

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context);

        var seconds = ((rank?.Bubbles ?? 0) - member.Bubbles) * 10;
        if (seconds > 0)
        {
            embed.WithTitle($"Time to become #{position}")
                .WithDescription(
                    $"To reach #{position} ({rank?.Bubbles ?? 0} Bubbles) on the server leaderboard, you have to spend `{TimeSpan.FromSeconds(seconds).AsCountdownTimespan()}` on skribbl.");
        }
        else
        {
            embed.WithTitle($"Time ahead of #{position}")
                .WithDescription(
                    $"You are `{TimeSpan.FromSeconds(seconds * -1).AsCountdownTimespan()}` ahead of #{position} ({rank?.Bubbles ?? 0} Bubbles) on the server leaderboard.");
        }

        await context.RespondAsync(embed);
    }
}