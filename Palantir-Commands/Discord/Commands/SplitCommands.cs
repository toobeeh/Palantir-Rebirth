using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands.Attributes;
using DSharpPlus.Commands.Trees.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Palantir_Commands.Discord.Checks;
using Palantir_Commands.Discord.Converters;
using Palantir_Commands.Discord.Extensions;
using Palantir_Commands.Services;
using Valmar;
using Valmar_Client.Grpc;

namespace Palantir_Commands.Discord.Commands;

[Command("boost")]
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
                    $"You can use splits to boost the droprate on skribbl using the `/dropboost` command.")
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

        var member = memberContext.Member;
        var memberAvailableBoosts = await splitsClient.GetAvailableSplitsAsync(new GetAvailableSplitsRequest { Login = member.Login });
        var boosts = await splitsClient.GetActiveDropboosts(new Empty()).ToListAsync();
        var droprate = await dropsClient.GetCurrentBoostFactorAsync(new Empty());
        var onlinePlayersCount = (await lobbiesClient.GetOnlinePlayers(new Empty()).ToListAsync()).Count;
        var bounds = await dropsClient.CalculateDropDelayBoundsAsync(new CalculateDelayRequest
        {
            OnlinePlayerCount = onlinePlayersCount,
            BoostFactor = droprate.Boost
        });
        
        var totalBoost = 1 + boosts.Sum(boost => boost.Factor - 1);
        var boostsSummary = boosts.Count == 0 ? "No drop boosts active" : string.Join("\n",
            boosts.Select(boost =>
                $"+{boost.Factor - 1 :0.#} ~ {(boost.StartDate.ToDateTimeOffset().AddSeconds(boost.DurationSeconds) - DateTimeOffset.Now).TotalMinutes :0}min remaining ~ {boost.Value} splits"));
        
        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithDescription(
                "Using splits, the drop rate can be boosted. The higher the drop rate, the more frequently drops appear on skribbl.\n" +
                "With the `/dropboost` command, you can start a drop boost once a week.\n _ _ \n**Active Boosts**\n" +
                $"{boostsSummary}\n" +
                (boosts.Count > 0 ? $"===========\nx{totalBoost :0.#} Boost active" : ""))
            .WithTitle("Current Drop Boosts");
        
        if(memberAvailableBoosts.ActiveDropboosts.Count > 0)
        {
            var description = string.Join("\n", 
                memberAvailableBoosts.ActiveDropboosts
                    .OrderBy(boost => boost.StartDate)
                    .Select((boost, index) => 
                        $"- x{boost.Factor - 1 :0.#} ~ {boost.Value} splits {PalantirFormatter.AsTypoId(index + 1)}\n" +
                        $"  started {Formatter.Timestamp(boost.StartDate.ToDateTimeOffset(), TimestampFormat.ShortDateTime)} ~ " +
                        $"cooldown ends {Formatter.Timestamp(boost.CooldownEndDate.ToDateTimeOffset(), TimestampFormat.ShortDateTime)}" ));
            
            embedBuilder.AddField("Your Boosts",
                description);
        }

        embedBuilder.AddField("Current Droprate",
            $"`⌚` Drops appear every {bounds.MinDelaySeconds}-{bounds.MaxDelaySeconds} seconds.");
        
        await context.RespondAsync(embedBuilder.Build());
    }

    /// <summary>
    /// Starts a new dropboost
    /// </summary>
    /// <param name="context"></param>
    /// <param name="factorSplits"></param>
    /// <param name="durationSplits"></param>
    /// <param name="cooldownSplits"></param>
    /// <param name="startMode"></param>
    /// <exception cref="Exception"></exception>
    [Command("start")]
    [DefaultGroupCommand]
    [RequirePalantirMember(MemberFlagMessage.Beta)]
    public async Task StartDropboost(CommandContext context, uint factorSplits = 2, uint durationSplits = 0, uint cooldownSplits = 0, DropboostStartMode startMode = DropboostStartMode.Check)
    {
        logger.LogTrace("StartDropboost(context, factorSplits={factorSplits}, durationSplits={durationSplits}, cooldownSplits={cooldownSplits}, startMode={startMode})", factorSplits, durationSplits, cooldownSplits, startMode);

        var member = memberContext.Member;
        var availableSplits = await splitsClient.GetAvailableSplitsAsync(new GetAvailableSplitsRequest { Login = member.Login });
        await StartDropboostInteraction(context, member, availableSplits, factorSplits, durationSplits, cooldownSplits, startMode);
    }

    /// <summary>
    /// Upgrades an existing dropboost
    /// </summary>
    /// <param name="context"></param>
    /// <param name="boostId"></param>
    /// <param name="factorSplitsIncrease"></param>
    /// <param name="durationSplitsIncrease"></param>
    /// <param name="cooldownSplitsIncrease"></param>
    /// <param name="startMode"></param>
    /// <exception cref="Exception"></exception>
    [Command("upgrade")]
    [TextAlias("ug")]
    [RequirePalantirMember(MemberFlagMessage.Beta)]
    public async Task UpgradeDropboost(CommandContext context, uint boostId, uint factorSplitsIncrease = 0, uint durationSplitsIncrease = 0, uint cooldownSplitsIncrease = 0, DropboostStartMode startMode = DropboostStartMode.Check)
    {
        logger.LogTrace("UpgradeDropboost(context, factorSplitsIncrease={factorSplitsIncrease}, durationSplitsIncrease={durationSplitsIncrease}, cooldownSplitsIncrease={cooldownSplitsIncrease}, startMode={startMode})", factorSplitsIncrease, durationSplitsIncrease, cooldownSplitsIncrease, startMode);

        var member = memberContext.Member;
        var availableSplits = await splitsClient.GetAvailableSplitsAsync(new GetAvailableSplitsRequest { Login = member.Login });
        var targetBoost = availableSplits.ActiveDropboosts.OrderBy(boost => boost.StartDate).ElementAtOrDefault((int)boostId - 1);
        if (targetBoost is null)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Invalid boost id",
                    "The specified boost does not exist. Use `/boost cooldown` to view your active boosts."));
            return;
        }
        
        await StartDropboostInteraction(context, member, availableSplits, factorSplitsIncrease, durationSplitsIncrease, cooldownSplitsIncrease, startMode, targetBoost);
    }

    private async Task StartDropboostInteraction(CommandContext context, MemberReply member,
        AvailableSplitsReply availableSplits, uint factorSplits = 2, uint durationSplits = 0, uint cooldownSplits = 0, DropboostStartMode startMode = DropboostStartMode.Check, ActiveDropboostReply? boostModify = null)
    {
        var splitsPrices = await splitsClient.GetBoostCostInformationAsync(new Empty());
        
        if(availableSplits.CanStartBoost == false)
        {
            var nextCooldown = availableSplits.ActiveDropboosts
                .OrderBy(boost => boost.CooldownEndDate)
                .First();
            
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, $"Cannot ${(boostModify is null ? "start" : "upgrade")} dropboost", 
                    $"You have used up all your splits.\nYou can ${(boostModify is null ? "start" : "upgrade")} your next boost with {nextCooldown.Value} splits at {Formatter.Timestamp(nextCooldown.CooldownEndDate.ToDateTimeOffset(), TimestampFormat.ShortDateTime)}."));
            return;
        }
        
        if(factorSplits + durationSplits + cooldownSplits > availableSplits.AvailableSplits)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Not enough splits", $"You do not have enough splits to ${(boostModify is null ? "start" : "upgrade")} this boost.\n Use `/splits` to view your splits."));
            return;
        }

        string? invalidSplitAmount = null;
        if(factorSplits % splitsPrices.FactorSplitCost != 0) invalidSplitAmount = "factor splits must be a multiple of " + splitsPrices.FactorSplitCost;
        if(durationSplits % splitsPrices.DurationSplitCost != 0) invalidSplitAmount = "duration splits must be a multiple of " + splitsPrices.DurationSplitCost;
        if(cooldownSplits % splitsPrices.CooldownSplitCost != 0) invalidSplitAmount = "cooldown splits must be a multiple of " + splitsPrices.CooldownSplitCost;
        
        if(invalidSplitAmount is not null)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Invalid split amount", $"The {invalidSplitAmount}."));
            return;
        }

        var description = $"You have `{availableSplits.AvailableSplits}` splits available.\n" +
                          $"Using splits, you can power up your boost.\n" +
                          $"Use `/splits` to learn more about your splits.\n_ _ \n" +
                          $"`🔥 Factor: +{splitsPrices.FactorSplitCost} splits => +{splitsPrices.FactorIncrease} factor`\n" +
                          $"`⌛ Duration: +{splitsPrices.DurationSplitCost} splits => +{splitsPrices.DurationIncreaseMinutes}min boost`\n" +
                          $"`💤 Cooldown: +{splitsPrices.CooldownSplitCost} splits => -{splitsPrices.CooldownIncreaseHours}h until next boost`\n";
        
        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle(boostModify is not null ? $"Upgrade your Dropboost from {Formatter.Timestamp(boostModify.StartDate.ToDateTimeOffset(), TimestampFormat.ShortDateTime)}" : "Start a new Dropboost")
            .WithDescription(description);

        var messageBuilder = new DiscordMessageBuilder()
            .AddEmbed(embedBuilder.Build());
        
        void UpdateComponents(string submitText, bool disable)
        {
            messageBuilder.ClearComponents();

            var minusFactor = new DiscordButtonComponent(ButtonStyle.Secondary, "-fac", "-", disable);
            var plusFactor = new DiscordButtonComponent(ButtonStyle.Secondary, "+fac", "+", disable);
            var labelFactor = new DiscordButtonComponent(ButtonStyle.Primary, "fac", 
                $"Boost Factor: {factorSplits} Splits (+{splitsPrices.DefaultFactor + factorSplits/splitsPrices.FactorSplitCost * splitsPrices.FactorIncrease :0.#}x)", true);

            var minusDur = new DiscordButtonComponent(ButtonStyle.Secondary, "-dur", "-", disable);
            var plusDur = new DiscordButtonComponent(ButtonStyle.Secondary, "+dur", "+", disable);
            var labelDur = new DiscordButtonComponent(ButtonStyle.Primary, "dur", 
                $"Boost Duration: {durationSplits} Splits (+{splitsPrices.DefaultDurationMinutes + durationSplits/splitsPrices.DurationSplitCost * splitsPrices.DurationIncreaseMinutes :0.#}min)", true);

            var minusCool = new DiscordButtonComponent(ButtonStyle.Secondary, "-cool", "-", disable);
            var plusCool = new DiscordButtonComponent(ButtonStyle.Secondary, "+cool", "+", disable);
            var labelCool = new DiscordButtonComponent(ButtonStyle.Primary, "cool", 
                $"Boost Cooldown: {cooldownSplits} Splits (-{splitsPrices.DefaultCooldownHours + cooldownSplits/splitsPrices.CooldownSplitCost * splitsPrices.CooldownIncreaseHours :0}h)", true);
            var start = new DiscordButtonComponent(ButtonStyle.Success, "start", submitText + " (" + (cooldownSplits + durationSplits + factorSplits) + "/" + availableSplits.AvailableSplits + " Splits selected)", disable);
            
            messageBuilder
                .AddComponents(minusFactor, labelFactor, plusFactor)
                .AddComponents(minusDur, labelDur, plusDur)
                .AddComponents(minusCool, labelCool, plusCool)
                .AddComponents(start);
        }
        
        UpdateComponents($"{(boostModify is null ? "Start" : "Upgrade")} Dropboost", false);
        await context.RespondAsync(messageBuilder);
        var response = await context.GetResponseAsync();
        var interactivity = context.Client.GetInteractivity();
        
        async Task SubmitBoost()
        {
            if (boostModify is not null)
            {
                await splitsClient.UpgradeDropboostAsync(new UpgradeDropboostRequest
                {
                    Login = member.Login,
                    FactorSplitsIncrease = (int)factorSplits,
                    DurationSplitsIncrease = (int)durationSplits,
                    CooldownSplitsIncrease = (int)cooldownSplits,
                    StartDate = boostModify.StartDate
                });
            }
            else
            {
                await splitsClient.StartDropboostAsync(new StartDropboostRequest
                {
                    FactorSplits = (int)factorSplits,
                    DurationSplits = (int)durationSplits,
                    CooldownSplits = (int)cooldownSplits,
                    Login = member.Login
                });
            }
                 
            UpdateComponents($"🔥 {(boostModify is null ? "Boosted" : "Upgraded")}!", true);
            await response.ModifyAsync(messageBuilder);
        }

        if (startMode == DropboostStartMode.Now)
        {
            await SubmitBoost();
            return;
        }
        
         // wait for interactions on the buttons
         var buttonEvent = await interactivity.WaitForButtonAsync(response, context.User, TimeSpan.FromMinutes(1));
         while(!buttonEvent.TimedOut)
         {
             await buttonEvent.Result.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);
             var buttonId = buttonEvent.Result.Id;

             if (buttonId == "start")
             {
                 await SubmitBoost();
                 return;
             }
             
             uint[] oldSplits = [factorSplits, durationSplits, cooldownSplits];
             var mode = buttonId.StartsWith('+') ? 1 : -1;
             if(buttonId.Contains("fac")) factorSplits = (uint)(factorSplits + mode * splitsPrices.FactorSplitCost);
             if(buttonId.Contains("dur")) durationSplits = (uint)(durationSplits + mode * splitsPrices.DurationSplitCost);
             if(buttonId.Contains("cool")) cooldownSplits = (uint)(cooldownSplits + mode * splitsPrices.CooldownSplitCost);
             
             if(factorSplits + durationSplits + cooldownSplits > availableSplits.AvailableSplits)
             {
                 factorSplits = oldSplits[0];
                 durationSplits = oldSplits[1];
                 cooldownSplits = oldSplits[2];
             }
             
             UpdateComponents($"{(boostModify is null ? "Start" : "Upgrade")} Dropboost", false);
             await response.ModifyAsync(messageBuilder);
             buttonEvent = await interactivity.WaitForButtonAsync(response, context.User, TimeSpan.FromMinutes(1));
         }
        
         // disable navigation after timeout
         UpdateComponents("Timed out", true);
         await response.ModifyAsync(messageBuilder);
    }
    
}