using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands.Attributes;
using DSharpPlus.Commands.Trees.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
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
        var inventory =
            await splitsClient.GetMemberSplitRewards(new GetMemberSplitRewardsRequest { Login = member.Login }).ToListAsync();

        const int batchSize = 10;
        var pages = inventory.Batch(batchSize).Select((batch, idx) => new
        {
            Page = idx + 1,
            Splits = batch
        }).Select(page =>
        {
            var embedBuilder = new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithDescription(
                    "Splits are rewards which you can obtain by participating in leagues or special occasions.\n" +
                    $"You can use splits to boost the droprate on skribbl using the `/droprate` command.")
                .WithTitle("Split Achievements");

            if (page.Page == 1 && page.Splits.Length == 0)
            {
                embedBuilder.AddField("No rewards", "You have not received any splits yet.");
            }

            else
            {
                foreach (var reward in page.Splits)
                {
                    var description = $"{(reward.Expired ? "`⛔`" : "")} {Formatter.Timestamp(reward.RewardDate.ToDateTimeOffset(), TimestampFormat.ShortDate)}\n" +
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
    
}