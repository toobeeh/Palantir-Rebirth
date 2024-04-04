using DSharpPlus;
using DSharpPlus.Commands;
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
using Valmar_Client.Grpc;

namespace Palantir_Commands.Discord.Commands;

[Command("split")]
[TextAlias("spl")]
public class SplitCommands(
    ILogger<SplitCommands> logger, 
    MemberContext memberContext,
    Drops.DropsClient dropsClient,
    Lobbies.LobbiesClient lobbiesClient,
    Splits.SplitsClient splitsClient)
{
    
    /// <summary>
    /// View your received splits
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="Exception"></exception>
    [Command("inventory")]
    [TextAlias("inv")]
    [RequirePalantirMember(MemberFlagMessage.Beta)]
    public async Task ViewSplitInventory(CommandContext context)
    {
        logger.LogTrace("ViewSplitInventory(context)");

        var member = memberContext.Member;
        var available = await splitsClient.GetAvailableSplitsAsync(new GetAvailableSplitsRequest { Login = member.Login });
        var inventory =
            await splitsClient.GetMemberSplitRewards(new GetMemberSplitRewardsRequest { Login = member.Login }).ToListAsync();
        var totalAmount = inventory.Sum(x => x.ValueOverride ?? x.Split.Value);

        const int batchSize = 12;
        var pages = inventory.OrderByDescending(reward => reward.RewardDate).Batch(batchSize).Select((batch, idx) => new
        {
            Page = idx + 1,
            Splits = batch
        }).Select(page =>
        {
            var embedBuilder = new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithDescription(
                    "Splits are rewards which you can obtain by participating in leagues or special occasions.\n" +
                    $"You can use splits to boost the droprate on skribbl using the `/split boost` command.")
                .WithTitle("Split Achievements");

            if (page.Page == 1 && page.Splits.Length == 0)
            {
                embedBuilder.AddField("No rewards", "You have not received any splits yet.");
            }

            else
            {
                embedBuilder.AddField("Overview:",
                    $"`📃` {totalAmount} total ~ {available.TotalSplits} unexpired ~ {available.AvailableSplits} available for boost\n_ _ \n_ _ ");
                
                foreach (var reward in page.Splits)
                {
                    var description = $"{(reward.Expired ? "`⛔`" : "`☑️`")} {Formatter.Timestamp(reward.RewardDate.ToDateTimeOffset(), TimestampFormat.ShortDate)}\n" +
                                      $" **{reward.ValueOverride ?? reward.Split.Value} Splits **\n" +
                                       $"{reward.Split.Description}" +
                                       $"{(reward.Comment is not null ? "\n`" + reward.Comment + "`" : "")}";
                    
                    embedBuilder.AddField($"{reward.Split.Name}", description, true);
                }
            }

            return embedBuilder;
        }).ToList();

        await context.RespondPalantirPaginationAsync(pages);
    }
    
    /// <summary>
    /// View a listing of currently active dropboosts
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="Exception"></exception>
    [Command("rate")]
    [TextAlias("rt")]
    [RequirePalantirMember(MemberFlagMessage.Beta)]
    public async Task ViewCurrentDropboosts(CommandContext context)
    {
        logger.LogTrace("ViewCurrentDropboosts(context)");

        var boosts = await splitsClient.GetActiveDropboosts(new Empty()).ToListAsync();
        var droprate = await dropsClient.GetCurrentBoostFactorAsync(new Empty());
        var onlinePlayersCount = (await lobbiesClient.GetOnlinePlayers(new Empty()).ToListAsync()).Count;
        var bounds = await dropsClient.CalculateDropDelayBoundsAsync(new CalculateDelayRequest
        {
            OnlinePlayerCount = onlinePlayersCount,
            BoostFactor = droprate.Boost
        });
        
        var totalBoost = 1 + boosts.Sum(boost => boost.Factor - 1);
        var boostsSummary = string.Join("\n",
            boosts.Select(boost =>
                $"x`{boost.Factor}` until {Formatter.Timestamp(boost.StartDate.ToDateTimeOffset().AddSeconds(boost.DurationSeconds))}"));
        
        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithDescription(
                "Using splits, the drop rate can be boosted. The higher the drop rate, the more frequently drops appear on skribbl.\n" +
                "With the `/split boost` command, you can start a drop boost once a week.\n _ _ \n**Active Boosts:**\n" +
                $"{boostsSummary}\n" +
                $"===========\nx`{totalBoost :0.#}` Boost active")
            .WithTitle("Current Drop Boosts");

        embedBuilder.AddField("Current Droprate",
            $"`⌚` Drops appear every {bounds.MinDelaySeconds}-{bounds.MaxDelaySeconds} seconds.");
        
        await context.RespondAsync(embedBuilder.Build());
    }
    
}