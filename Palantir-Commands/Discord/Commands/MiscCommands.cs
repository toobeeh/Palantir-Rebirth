using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.TextCommands.Attributes;
using DSharpPlus.Commands.Trees.Attributes;
using DSharpPlus.Entities;
using Google.Protobuf.WellKnownTypes;
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
    
    /// <summary>
    /// Shows a statistics of your bubble progress
    /// </summary>
    /// <param name="context"></param>
    /// <param name="mode">The display mode: either day, week or month</param>
    [Command("statistics"), TextAlias("stat"), RequirePalantirMember]
    public async Task ViewBubbleProgress(CommandContext context, BubbleProgressIntervalMode mode = BubbleProgressIntervalMode.Day)
    {
        logger.LogTrace("ViewBubbleProgress(context, mode={mode})", mode);

        var statStart = mode == BubbleProgressIntervalMode.Day ? DateTimeOffset.UtcNow.AddMonths(-1) :
            mode == BubbleProgressIntervalMode.Week ? DateTimeOffset.UtcNow.AddMonths(-6) :
            new DateTimeOffset(2020, 8, 1, 0, 0, 0, TimeSpan.Zero);
        
        var member = memberContext.Member;
        var stats = await statsClient.GetBubbleProgressAsync(new GetBubbleProgressMessage
        {
            Login = member.Login,
            StartDate = Timestamp.FromDateTimeOffset(statStart),
            EndDate = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            Interval = mode
        });
        
        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor("Viewing your bubble progress");
        
        if (stats.Entries.Count <= 1)
        {
            embed.WithTitle("Hello, world!")
                .WithDescription("You have not collected any bubbles yet.\nCheck again tomorrow!");
        }
        else
        {
            var days = stats.Entries.Select(entry => new { entry.Bubbles, Date = entry.Date.ToDateTimeOffset() }).ToList();
            embed.WithTitle($"Bubble Gain from {Formatter.Timestamp(days.First().Date, TimestampFormat.ShortDate)} to {Formatter.Timestamp(days.Last().Date, TimestampFormat.ShortDate)}");

            const int width = 30;
            var start = days.First().Bubbles;
            var diff = start - days.Last().Bubbles;
            var bubblesPerSpace = diff/width;
            var dayDiff = (statStart - DateTimeOffset.UtcNow).TotalDays;

            embed.AddField("Total collected:",
                $"{Math.Abs(diff)} Bubbles ({Math.Abs(TimeSpan.FromSeconds(diff * 10).TotalHours):0.##}h)\n");
            
            embed.AddField("Average per day:",
                $"{diff / dayDiff :0} Bubbles ({TimeSpan.FromSeconds((diff / dayDiff) * 10).TotalHours :0.#}h)\n");
            
            var graph = "```css\n";
            
            int lastBubbles = 0;
            for (var i = 0; i < days.Count; i++)
            {
                var day = days[i];
                var position = 1 + Convert.ToInt16((start - day.Bubbles) / bubblesPerSpace);
                graph += $"{day.Date:d} {new string('█', position)}";
                
                if(i == 0 || i == days.Count - 1)
                {
                    graph += $"  @{day.Bubbles}";
                }
                
                if(day.Bubbles > lastBubbles && i != 0)
                {
                    graph += $"  +{day.Bubbles - lastBubbles}";
                }
                
                lastBubbles = day.Bubbles;
                graph += "\n";
            }
            
            graph += "```";
            embed.WithDescription(graph);
        }

        await context.RespondAsync(embed.Build());
    }
    
}