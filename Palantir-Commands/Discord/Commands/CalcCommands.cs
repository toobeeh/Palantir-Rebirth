using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.TextCommands.Attributes;
using DSharpPlus.Commands.Trees.Attributes;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Palantir_Commands.Discord.Checks;
using Palantir_Commands.Discord.Extensions;
using Palantir_Commands.Services;
using Valmar;

namespace Palantir_Commands.Discord.Commands;

[Command("calculate"), TextAlias("calc")]
public class CalcCommands(ILogger<CalcCommands> logger, MemberContext memberContext, Guilds.GuildsClient guildsClient, Stats.StatsClient statsClient)
{
    
    [Command("bubbles"), TextAlias("b")]
    public async Task CalculateBubbles(CommandContext context, uint amount)
    {
        logger.LogTrace("CalculateBubbles(amount={amount})", amount);
        
        var seconds = amount * 10;
        var time = TimeSpan.FromSeconds(seconds);
        
        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"Time to reach {amount} bubbles")
            .WithDescription(@$"To collect {amount} more bubbles, you have to spend `{time.AsCountdownTimespan()}` on skribbl.");

        await context.RespondAsync(embed);
    }
    
    [Command("rank"), TextAlias("rk"), RequirePalantirMember, RequireGuild]
    public async Task CalculateLeaderboard(CommandContext context, uint position)
    {
        logger.LogTrace("CalculateLeaderboard(position={position})", position);
        
        var guild = await guildsClient.GetGuildByDiscordIdAsync(new GetGuildByIdMessage { DiscordId = (long) context.Guild!.Id });
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
                .WithDescription(@$"To reach #{position} ({rank?.Bubbles ?? 0} Bubbles) on the server leaderboard, you have to spend `{TimeSpan.FromSeconds(seconds).AsCountdownTimespan()}` on skribbl.");
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