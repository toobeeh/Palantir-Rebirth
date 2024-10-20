using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using tobeh.Palantir.Commands.Checks;
using tobeh.Palantir.Commands.Extensions;
using tobeh.TypoContentService;
using tobeh.TypoContentService.Client.Util;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Commands.Commands;

public class MiscCommands(
    ILogger<OutfitCommands> logger,
    MemberContext memberContext,
    Guilds.GuildsClient guildsClient,
    Stats.StatsClient statsClient,
    Inventory.InventoryClient inventoryClient,
    Sprites.SpritesClient spriteClient,
    Scenes.ScenesClient scenesClient,
    Splits.SplitsClient splitsClient,
    ImageGenerator.ImageGeneratorClient imageGeneratorClient
)
{
    /// <summary>
    /// View an overview of your typo account
    /// </summary>
    /// <param name="context"></param>
    [Command("inventory"), TextAlias("inv"), RequirePalantirMember]
    public async Task ViewInventory(CommandContext context)
    {
        logger.LogTrace("ViewInventory(context)");

        await context.DeferResponseAsync();
        var member = memberContext.Member;

        // start tasks in parallel
        var bubbleCreditTask =
            inventoryClient.GetBubbleCreditAsync(new GetBubbleCreditRequest { Login = member.Login });
        var dropCreditTask = inventoryClient.GetDropCreditAsync(new GetDropCreditRequest { Login = member.Login });
        var spritesTask = spriteClient.GetAllSprites(new Empty()).ToListAsync();
        var scenesTask = scenesClient.GetAllScenes(new Empty()).ToListAsync();
        var spriteInvTask = inventoryClient.GetSpriteInventory(new GetSpriteInventoryRequest { Login = member.Login })
            .ToListAsync();
        var sceneInvTask =
            inventoryClient.GetSceneInventoryAsync(new GetSceneInventoryRequest { Login = member.Login });
        var awardInvTask =
            inventoryClient.GetAwardInventoryAsync(new GetAwardInventoryMessage { Login = member.Login });
        var splitInvTask = splitsClient.GetAvailableSplitsAsync(new GetAvailableSplitsRequest { Login = member.Login });
        var firstSeenTask = inventoryClient.GetFirstSeenDateAsync(new GetFirstSeenDateRequest { Login = member.Login });
        var awardPackInfoTask =
            inventoryClient.GetAwardPackLevelAsync(new GetAwardPackLevelMessage { Login = member.Login });
        var spriteSlotCountTask =
            inventoryClient.GetSpriteSlotCountAsync(new GetSpriteSlotCountRequest { Login = member.Login });
        var splitRewardsTask = splitsClient
            .GetMemberSplitRewards(new GetMemberSplitRewardsRequest { Login = member.Login }).ToListAsync();

        // collect results
        var bubbleCredit = await bubbleCreditTask;
        var dropCredit = await dropCreditTask;
        var spriteInv = await spriteInvTask;
        var scenes = (await scenesTask).ToDictionary(scene => scene.Id);
        var sprites = (await spritesTask).ToDictionary(sprite => sprite.Id);
        var sceneInv = await sceneInvTask;
        var awardInv = await awardInvTask;
        var splitInv = await splitInvTask;
        var firstSeen = await firstSeenTask;
        var awardPackInfo = await awardPackInfoTask;
        var spriteSlotCount = await spriteSlotCountTask;
        var splitRewards = await splitRewardsTask;

        // build embed
        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"Inventory of {member.Username}");

        Dictionary<MemberFlagMessage, string> flagBadgeMap = new()
        {
            { MemberFlagMessage.Admin, "🛠️ Admin" },
            { MemberFlagMessage.Moderator, "🛡️ Moderator" },
            { MemberFlagMessage.Beta, "🧪 Beta Tester" },
            { MemberFlagMessage.Booster, "🔥 Server Booster" },
            { MemberFlagMessage.BubbleFarming, "⚠️ Bubble Farming" },
            { MemberFlagMessage.PermaBan, "⚠️ Typo Ban" },
            { MemberFlagMessage.DropBan, "⚠️ Drop Ban" },
            { MemberFlagMessage.Patron, "💙 Patron" },
            { MemberFlagMessage.Patronizer, "❤️‍🔥 Patronizer" }
        };

        var userFlagMessages = member.MappedFlags
            .Select(flag =>
            {
                flagBadgeMap.TryGetValue(flag, out var flagDesc);
                return flagDesc;
            })
            .OfType<string>()
            .ToList();

        if (firstSeen.FirstSeen.ToDateTimeOffset() < new DateTimeOffset(2020, 9, 1, 0, 0, 0, TimeSpan.Zero))
        {
            userFlagMessages.Add("💎 Early Member");
        }

        if (userFlagMessages.Count > 0)
            embed.AddField("Badges", "```js\n" + string.Join("\n", userFlagMessages) + "\n```", true);

        var splitsTotalCount = splitRewards.Sum(reward => reward.ValueOverride ?? reward.Split.Value);
        var splitsAvailableCount = splitRewards.Where(split => !split.Expired)
            .Sum(reward => reward.ValueOverride ?? reward.Split.Value);
        embed.AddField("Collections",
            $"```asciidoc\n- {spriteInv.Count} sprites\n- {sceneInv.Scenes.Count(scene => scene.SceneShift is null)} scenes\n- {awardInv.ReceivedAwards.Count} awards\n- {splitsAvailableCount} splits ({splitsTotalCount} total)\n- {spriteSlotCount.UnlockedSlots} slots unlocked\n```",
            true);

        // field as spacer
        embed.AddField("_ _", "_ _");

        embed.AddField("Bubble Stats",
            $"```asciidoc\n- {bubbleCredit.BubblesAmount} collected\n- {bubbleCredit.AvailableCredit} available\n- First seen {firstSeen.FirstSeen.ToDateTimeOffset():d}```\n_ _",
            true);
        embed.AddField("Drop Stats",
            $"```asciidoc\n- {dropCredit.Credit:0.#} total value\n- {dropCredit.Count} drops caught\n- {spriteSlotCount.DropsUntilNextSlot:0.#} until next slot```",
            true);

        var activeSprites = spriteInv.Where(slot => slot.Slot > 0 && slot.SpriteId > 0)
            .Select(slot => sprites[slot.SpriteId].Name).ToList();
        var spritesText = activeSprites.Count > 0
            ? string.Join(", ", activeSprites)
            : "None";
        var sceneText = sceneInv.ActiveId is { } activeId ? scenes[activeId].Name : "Empty";
        var emojiText = member.PatronEmoji is { } emoji && !string.IsNullOrWhiteSpace(emoji) ? $"\nEmoji: {emoji}" : "";
        embed.AddField("Skribbl Outfit", $"```yaml\nScene: {sceneText}\nSprites: {spritesText}{emojiText}```");

        var boostAvailable = splitInv.CanStartBoost;
        var packAvailable = member.NextAwardPackDate.ToDateTimeOffset() < DateTimeOffset.UtcNow;
        var boostText = boostAvailable
            ? "Dropboost available!"
            : $"Next boost in {(splitInv.ActiveDropboosts.Select(boost => boost.CooldownEndDate).Order().First().ToDateTimeOffset() - DateTimeOffset.UtcNow).AsCountdownTimespan()}";
        var awardPackText = packAvailable
            ? "Award pack available!"
            : $"Next pack in {(member.NextAwardPackDate.ToDateTimeOffset() - DateTimeOffset.UtcNow).AsCountdownTimespan()}";
        embed.AddField("Recent Activity",
            $"```md\n{(packAvailable ? ">" : "-")} {awardPackText}\n{(boostAvailable ? ">" : "-")} {boostText}\n- {awardPackInfo.CollectedBubbles} bubbles / last week```");

        var colorMaps = spriteInv
            .Where(spt => spt.ColorShift != null && spt.Slot > 0)
            .Select(slot => new ColorMapMessage { HueShift = slot.ColorShift ?? 100, SpriteId = slot.SpriteId });

        var combo = spriteInv.Where(slot => slot.Slot > 0).OrderBy(slot => slot.Slot).Select(slot => slot.SpriteId);
        var imageFile = await imageGeneratorClient.GenerateSpriteCombo(new GenerateComboMessage()
            { SpriteIds = { combo }, ColorMaps = { colorMaps } }).CollectFileChunksAsync();

        await context.RespondAsync(
            embed.ToMessageBuilderWithAttachmentImage(imageFile.FileName, imageFile.Data));
    }


    /// <summary>
    /// View the leaderboard of the current server
    /// </summary>
    /// <param name="context"></param>
    /// <param name="mode">The ranking mode - either bubbles, awards or drops</param>
    /// <param name="searchValue">When a search value is provided, the leaderboard shows the page with the closest scores first.</param>
    [Command("leaderboard"), TextAlias("lb"), RequireGuild]
    public async Task ViewLeaderboard(CommandContext context, LeaderboardMode mode = LeaderboardMode.Bubbles,
        uint? searchValue = null)
    {
        logger.LogTrace("ViewLeaderboard(context, mode={mode})", mode);

        var leaderboard = await statsClient.GetLeaderboardAsync(new GetLeaderboardMessage
        {
            GuildId = (long)context.Guild!.Id,
            Mode = mode
        });

        const int batchSize = 12;
        var batches = leaderboard.Entries.Chunk(batchSize).Select((batch, idx) => new
        {
            Page = idx + 1,
            Ranks = batch.ToList()
        }).ToList();

        var pages = batches.Select(page =>
        {
            var embed = new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithAuthor("Viewing the leaderboard of this server")
                .WithDescription(
                    $"This leaderboard lists all members which are connected to this server, ranked by their {(mode switch
                    {
                        LeaderboardMode.Bubbles => "bubble",
                        LeaderboardMode.Awards => "award",
                        _ => "drop"
                    })} score.")
                .WithTitle($"{(mode switch
                {
                    LeaderboardMode.Bubbles => "Bubble",
                    LeaderboardMode.Awards => "Award",
                    _ => "Drop"
                })} Leaderboard");

            page.Ranks.ForEach(rank => embed.AddField($"#{rank.Rank} _ _-_ _ {rank.Username}", $"```js\n" +
                $"Bubbles: {rank.Bubbles}\nAward Score: {rank.AwardScore}\nDrops: {rank.Drops:0.#}\n" +
                $"```\n_ _", true));

            return embed;
        }).ToList();

        int? startPage = searchValue is { } minVal
            ? batches.FirstOrDefault(batch => batch.Ranks.Any(rank =>
                (mode == LeaderboardMode.Bubbles ? rank.Bubbles : rank.Drops) <= minVal))?.Page
            : null;

        await context.RespondPalantirPaginationAsync(pages, "Page", startPage);
    }

    /// <summary>
    /// Show a statistic of your bubble progress
    /// </summary>
    /// <param name="context"></param>
    /// <param name="mode">The display mode of the statistics</param>
    [Command("statistics"), TextAlias("stat"), RequirePalantirMember]
    public async Task ViewBubbleProgress(CommandContext context,
        BubbleProgressIntervalMode mode = BubbleProgressIntervalMode.Day)
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
            var days = stats.Entries.Select(entry => new { entry.Bubbles, Date = entry.Date.ToDateTimeOffset() })
                .ToList();
            embed.WithTitle(
                $"Bubble Gain from {Formatter.Timestamp(days.First().Date, TimestampFormat.ShortDate)} to {Formatter.Timestamp(days.Last().Date, TimestampFormat.ShortDate)}");

            const int width = 30;
            var start = days.First().Bubbles;
            var diff = days.Last().Bubbles - start;

            if (diff <= 0)
            {
                await context.RespondAsync(new DiscordEmbedBuilder()
                    .WithPalantirErrorPresets(context, "No bubbles collected",
                        "You have not collected any bubbles in that timespan."));
                return;
            }

            var bubblesPerSpace = diff / width;
            var dayDiff = (DateTimeOffset.UtcNow - statStart).TotalDays;

            embed.AddField("Total collected:",
                $"{diff} Bubbles ({Math.Abs(TimeSpan.FromSeconds(diff * 10).TotalHours):0.##}h)\n");

            embed.AddField("Average per day:",
                $"{diff / dayDiff:0} Bubbles ({TimeSpan.FromSeconds((diff / dayDiff) * 10).TotalHours:0.#}h)\n");

            var graph = "```css\n";

            var lastBubbles = 0;
            for (var i = 0; i < days.Count; i++)
            {
                var day = days[i];
                var position = 1 + Convert.ToInt16((day.Bubbles - start) / bubblesPerSpace);
                graph += $"{day.Date:d} {new string('█', position)}";

                if (i == 0 || i == days.Count - 1)
                {
                    graph += $"  @{day.Bubbles}";
                }

                if (day.Bubbles > lastBubbles && i != 0)
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