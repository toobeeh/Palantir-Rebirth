using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.TextCommands.Attributes;
using DSharpPlus.Commands.Trees.Attributes;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Palantir_Commands.Discord.Checks;
using Palantir_Commands.Discord.Extensions;
using Palantir_Commands.Services;
using Valmar;

namespace Palantir_Commands.Discord.Commands;

public class MiscCommands(
    ILogger<OutfitCommands> logger,
    MemberContext memberContext,
    Guilds.GuildsClient guildsClient,
    Stats.StatsClient statsClient
)
{
    /// <summary>
    /// Views the leaderboard of the server
    /// </summary>
    /// <param name="context"></param>
    /// <param name="mode">The ranking mode - either bubble or drops</param>
    /// <param name="searchValue">When a search value is provided, the leaderboard shows the page with the closest scores first.</param>
    [Command("leaderboard"), TextAlias("lb"), RequireGuild]
    public async Task ViewLeaderboard(CommandContext context, LeaderboardMode mode = LeaderboardMode.Bubbles, int? searchValue = null)
    {
        logger.LogTrace("ViewLeaderboard(context, mode={mode})", mode);
        
        var guild = await guildsClient.GetGuildByDiscordIdAsync(new GetGuildByIdMessage { DiscordId = (long) context.Guild!.Id });
        var leaderboard = await statsClient.GetLeaderboardAsync(new GetLeaderboardMessage
        {
            GuildToken = guild.ObserveToken,
            Mode = mode
        });
        
        const int batchSize = 12;
        var batches = leaderboard.Entries.Batch(batchSize).Select((batch, idx) => new
        {
            Page = idx + 1,
            Ranks = batch.ToList()
        }).ToList();
            
        var pages = batches.Select(page =>
        {
            var embed = new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithAuthor("Viewing the leaderboard of this server")
                .WithDescription($"This leaderboard lists all members which are connected to this server, ranked by their {(mode == LeaderboardMode.Bubbles ? "bubble" : "drop")} score.")
                .WithTitle($"{(mode == LeaderboardMode.Bubbles ? "Bubble" : "Drop")} Leaderboard");

            page.Ranks.ForEach(rank => embed.AddField($"#{rank.Rank} _ _-_ _ {rank.Username}", $"```js\n" +
                $"Bubbles: {rank.Bubbles}\nDrops: {rank.Drops}\n" +
                $"```\n_ _", true));

            return embed;
        }).ToList();
        
        int? startPage = searchValue is {} minVal ? batches.FirstOrDefault(batch => batch.Ranks.Any(rank =>
            (mode == LeaderboardMode.Bubbles ? rank.Bubbles : rank.Drops) <= minVal))?.Page : null;

        await context.RespondPalantirPaginationAsync(pages, "Page", startPage);
    }
    
}