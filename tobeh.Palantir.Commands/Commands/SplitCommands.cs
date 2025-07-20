using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using tobeh.Palantir.Commands.Checks;
using tobeh.Palantir.Commands.Converters;
using tobeh.Palantir.Commands.Extensions;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Commands.Commands;

/// <summary>
/// View and manage splits and dropboosts
/// </summary>
/// <param name="logger"></param>
/// <param name="memberContext"></param>
/// <param name="dropsClient"></param>
/// <param name="lobbiesClient"></param>
/// <param name="splitsClient"></param>
[Command("boost"), TextAlias("bs")]
public class SplitCommands(
    ILogger<SplitCommands> logger,
    MemberContext memberContext,
    Drops.DropsClient dropsClient,
    Lobbies.LobbiesClient lobbiesClient,
    Splits.SplitsClient splitsClient)
{
    /// <summary>
    /// View all received split rewards
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="Exception"></exception>
    [Command("inventory"), TextAlias("inv"), RequirePalantirMember]
    public async Task ViewSplitInventory(CommandContext context)
    {
        logger.LogTrace("ViewSplitInventory(context)");

        var member = memberContext.Member;
        var available =
            await splitsClient.GetAvailableSplitsAsync(new GetAvailableSplitsRequest { Login = member.Login });
        var inventory =
            await splitsClient.GetMemberSplitRewards(new GetMemberSplitRewardsRequest { Login = member.Login })
                .ToListAsync();
        var totalAmount = inventory.Sum(x => x.ValueOverride ?? x.Split.Value);

        var rewards = inventory.Select(reward => new
        {
            Title = reward.Split.Name,
            Description = $"- {reward.ValueOverride ?? reward.Split.Value} splits\n" +
                          $"- On {reward.RewardDate.ToDateTimeOffset():d} ({(reward.Expired ? "⛔ Expired" : "✔️ Available")})\n" +
                          $"# {reward.Comment ?? reward.Split.Description}"
        });

        const int batchSize = 8;
        var pages = inventory.OrderByDescending(reward => reward.RewardDate).Select(reward => new
        {
            Title = $"{reward.Split.Name} _ _ `{(reward.Expired ? "⛔ Expired" : "✔️ Available")}`",
            Description =
                $"{Formatter.Colorize("-", reward.Expired ? AnsiColor.Magenta : AnsiColor.Cyan)} {reward.ValueOverride ?? reward.Split.Value} splits\n" +
                $"{Formatter.Colorize("-", reward.Expired ? AnsiColor.Magenta : AnsiColor.Cyan)} On {reward.RewardDate.ToDateTimeOffset():d}\n" +
                $" \n" +
                $"{Formatter.Colorize(reward.Comment ?? reward.Split.Description, reward.Expired ? AnsiColor.Magenta : AnsiColor.Cyan)}"
        }).Chunk(batchSize).Select((batch, idx) => new
        {
            Page = idx + 1,
            Splits = batch
        }).Select(page =>
        {
            var embedBuilder = new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithDescription(
                    "Splits are rewards which you can obtain by participating in leagues or special occasions.\n" +
                    $"You can use splits to boost the droprate on skribbl using the `/boost start` command.")
                .WithTitle("Split Achievement Inventory");

            if (page.Page == 1 && page.Splits.Length == 0)
            {
                embedBuilder.AddField("No achievements", "You have not received any splits yet.");
            }

            else
            {
                embedBuilder.AddField("Overview",
                    $"`📃` {totalAmount} total ~ {available.TotalSplits} available\n_ _ \n_ _ ");

                embedBuilder.WithDualColumnFields(page.Splits,
                    item => item.Title,
                    item => $"```ansi\n{item.Description}\n```");
            }

            return embedBuilder;
        }).ToList();

        await context.RespondPalantirPaginationAsync(pages);
    }

    /// <summary>
    /// View a list of your split cooldowns
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="Exception"></exception>
    [Command("cooldown"), TextAlias("cd"), RequirePalantirMember]
    public async Task ViewSplitCooldowns(CommandContext context)
    {
        logger.LogTrace("ViewSplitCooldowns(context)");

        var member = memberContext.Member;
        var memberAvailableBoosts =
            await splitsClient.GetAvailableSplitsAsync(new GetAvailableSplitsRequest { Login = member.Login });

        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithDescription(
                "When you use splits to start a dropboost, they have a default cooldown of seven days.\n" +
                "You can modify listed boosts with the command `/boost upgrade <id>`.\n" +
                "The splits listed here are currently in cooldown.")
            .WithTitle("Split Cooldowns");

        embedBuilder.AddField("Overview",
            $"`📃` {memberAvailableBoosts.TotalSplits} splits ~ {memberAvailableBoosts.AvailableSplits} ready to boost");

        if (memberAvailableBoosts.ActiveDropboosts.Count > 0)
        {
            var description = string.Join("\n",
                memberAvailableBoosts.ActiveDropboosts
                    .OrderBy(boost => boost.StartDate)
                    .Select((boost, index) =>
                        $"{(index + 1).AsTypoId()} +{boost.Factor - 1:0.#} ~ {boost.Value} splits\n" +
                        $"  started {Formatter.Timestamp(boost.StartDate.ToDateTimeOffset(), TimestampFormat.ShortDateTime)}\n" +
                        $"  cooldown ends {Formatter.Timestamp(boost.CooldownEndDate.ToDateTimeOffset(), TimestampFormat.ShortDateTime)}"));

            embedBuilder.AddField("Your Cooldowns",
                description);
        }
        else
        {
            embedBuilder.AddField("No Cooldowns", "All your splits are available!");
        }

        await context.RespondAsync(embedBuilder.Build());
    }

    /// <summary>
    /// View a list of currently active dropboosts
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="Exception"></exception>
    [Command("rate"), TextAlias("rt"), RequirePalantirMember]
    public async Task ViewCurrentDropboosts(CommandContext context)
    {
        logger.LogTrace("ViewCurrentDropboosts(context)");

        var member = memberContext.Member;
        var memberAvailableBoosts =
            await splitsClient.GetAvailableSplitsAsync(new GetAvailableSplitsRequest { Login = member.Login });
        var boosts = await splitsClient.GetActiveDropboosts(new Empty()).ToListAsync();
        var droprate = await dropsClient.GetCurrentBoostFactorAsync(new Empty());

        var lobbies = await lobbiesClient.GetOnlineLobbyPlayers(new GetOnlinePlayersRequest())
            .ToDictionaryAsync(p => p.LobbyId);
        var onlinePlayersCount = lobbies.Values.SelectMany(l => l.Members.Select(m => m.Login)).Distinct().Count();

        var bounds = await dropsClient.CalculateDropDelayBoundsAsync(new CalculateDelayRequest
        {
            OnlinePlayerCount = onlinePlayersCount,
            BoostFactor = droprate.Boost
        });

        var totalBoost = 1 + boosts.Sum(boost => boost.Factor - 1);
        var boostsSummary = boosts.Count == 0
            ? "No drop boosts active"
            : string.Join("\n",
                boosts.Select(boost =>
                    $"+{boost.Factor - 1:0.#} ~ {(boost.StartDate.ToDateTimeOffset().AddSeconds(boost.DurationSeconds) - DateTimeOffset.Now).TotalMinutes:0}min remaining ~ {boost.Value} splits"));

        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithDescription(
                "Using splits, the drop rate can be boosted. The higher the drop rate, the more frequently drops appear on skribbl.\n" +
                "With the `/boost start` command, you can start a drop boost once a week.\n _ _ \n**Active Boosts**\n" +
                $"{boostsSummary}\n" +
                (boosts.Count > 0 ? $"===========\nx{totalBoost:0.#} Boost active" : ""))
            .WithTitle("Current Drop Boosts");

        embedBuilder.AddField("Online Players", $"`👥` {onlinePlayersCount} people playing skribbl");

        embedBuilder.AddField("Current Droprate",
            $"`⌚` Drops appear every {bounds.MinDelaySeconds}-{bounds.MaxDelaySeconds} seconds.");

        await context.RespondAsync(embedBuilder.Build());
    }

    /// <summary>
    /// Start a new dropboost with smart split distribution.
    /// If instant cooldown is possible, splits are used for that with highest priority.
    /// Remaining splits are first put into factor, and the rest into duration.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="startMode">Choose "now" if you want to skip a review of your selected boost</param>
    /// <exception cref="Exception"></exception>
    [Command("smart"), RequirePalantirMember]
    public async Task StartSmartDropboost(CommandContext context,
        DropboostStartMode startMode = DropboostStartMode.Check)
    {
        logger.LogTrace(
            "StartDropboost(context, startMode={startMode})", startMode);

        var member = memberContext.Member;
        var splitsPrices = await splitsClient.GetBoostCostInformationAsync(new Empty());
        var availableSplits =
            await splitsClient.GetAvailableSplitsAsync(new GetAvailableSplitsRequest { Login = member.Login });

        var factorSplits = 0;
        var durationSplits = 0;
        var cooldownSplits = 0;
        var remainingSplits = availableSplits.AvailableSplits;

        var requiredSplitsForInstantCooldown = splitsPrices.CooldownSplitCost *
                                               (7 * 24 / splitsPrices.DefaultCooldownHours);

        // if instant cooldown possible, prefer that
        if (availableSplits.AvailableSplits >= requiredSplitsForInstantCooldown)
        {
            cooldownSplits = requiredSplitsForInstantCooldown;
            remainingSplits -= cooldownSplits;
        }

        // give as much factor splits as possible remaining
        factorSplits = (remainingSplits / splitsPrices.FactorSplitCost) * splitsPrices.FactorSplitCost;
        remainingSplits -= factorSplits;

        // give as much duration splits as possible remaining
        durationSplits = (remainingSplits / splitsPrices.DurationSplitCost) * splitsPrices.DurationSplitCost;

        await StartDropboostInteraction(context, member, availableSplits, (uint)factorSplits, (uint)durationSplits,
            (uint)cooldownSplits,
            startMode);
    }

    /// <summary>
    /// Start a new dropboost
    /// </summary>
    /// <param name="context"></param>
    /// <param name="factorSplits">The amount of splits that will be used to increase the droprate</param>
    /// <param name="durationSplits">The amount of splits that will be used to increase the boost time duration</param>
    /// <param name="cooldownSplits">The amount of splits that will be used to lower the cooldown of the used splits</param>
    /// <param name="startMode">Choose "now" if you want to skip a review of your selected boost</param>
    /// <exception cref="Exception"></exception>
    [Command("start"), DefaultGroupCommand, RequirePalantirMember]
    public async Task StartDropboost(CommandContext context, uint factorSplits = 2, uint durationSplits = 0,
        uint cooldownSplits = 0, DropboostStartMode startMode = DropboostStartMode.Check)
    {
        logger.LogTrace(
            "StartDropboost(context, factorSplits={factorSplits}, durationSplits={durationSplits}, cooldownSplits={cooldownSplits}, startMode={startMode})",
            factorSplits, durationSplits, cooldownSplits, startMode);

        var member = memberContext.Member;
        var availableSplits =
            await splitsClient.GetAvailableSplitsAsync(new GetAvailableSplitsRequest { Login = member.Login });
        await StartDropboostInteraction(context, member, availableSplits, factorSplits, durationSplits, cooldownSplits,
            startMode);
    }

    /// <summary>
    /// Upgrades an existing dropboost
    /// </summary>
    /// <param name="context"></param>
    /// <param name="boostId"></param>
    /// <param name="factorSplitsIncrease">The amount of splits that will be used to increase the droprate</param>
    /// <param name="durationSplitsIncrease">The amount of splits that will be used to increase the boost time duration</param>
    /// <param name="cooldownSplitsIncrease">The amount of splits that will be used to lower the cooldown of the used splits</param>
    /// <param name="startMode">Choose "now" if you want to skip a review of your selected boost</param>
    /// <exception cref="Exception"></exception>
    [Command("upgrade"), TextAlias("ug"), RequirePalantirMember, RequireGuild]
    public async Task UpgradeDropboost(CommandContext context, uint boostId, uint factorSplitsIncrease = 0,
        uint durationSplitsIncrease = 0, uint cooldownSplitsIncrease = 0,
        DropboostStartMode startMode = DropboostStartMode.Check)
    {
        logger.LogTrace(
            "UpgradeDropboost(context, factorSplitsIncrease={factorSplitsIncrease}, durationSplitsIncrease={durationSplitsIncrease}, cooldownSplitsIncrease={cooldownSplitsIncrease}, startMode={startMode})",
            factorSplitsIncrease, durationSplitsIncrease, cooldownSplitsIncrease, startMode);

        var member = memberContext.Member;
        var availableSplits =
            await splitsClient.GetAvailableSplitsAsync(new GetAvailableSplitsRequest { Login = member.Login });
        var targetBoost = availableSplits.ActiveDropboosts.OrderBy(boost => boost.StartDate)
            .ElementAtOrDefault((int)boostId - 1);
        if (targetBoost is null)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Invalid boost id",
                    "The specified boost does not exist. Use `/boost cooldown` to view your active boosts."));
            return;
        }

        await StartDropboostInteraction(context, member, availableSplits, factorSplitsIncrease, durationSplitsIncrease,
            cooldownSplitsIncrease, startMode, targetBoost);
    }

    private async Task StartDropboostInteraction(CommandContext context, MemberReply member,
        AvailableSplitsReply availableSplits, uint factorSplits = 2, uint durationSplits = 0, uint cooldownSplits = 0,
        DropboostStartMode startMode = DropboostStartMode.Check, ActiveDropboostReply? boostModify = null)
    {
        var splitsPrices = await splitsClient.GetBoostCostInformationAsync(new Empty());

        if (availableSplits.CanStartBoost == false)
        {
            var nextCooldown = availableSplits.ActiveDropboosts
                .OrderBy(boost => boost.CooldownEndDate)
                .First();

            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, $"Cannot {(boostModify is null ? "start" : "upgrade")} dropboost",
                    $"You have used up all your splits.\nYou can {(boostModify is null ? "start" : "upgrade")} your next boost with {nextCooldown.Value} splits at {Formatter.Timestamp(nextCooldown.CooldownEndDate.ToDateTimeOffset(), TimestampFormat.ShortDateTime)}."));
            return;
        }

        var neededSplits = factorSplits + durationSplits + cooldownSplits;
        if (neededSplits > availableSplits.AvailableSplits)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Not enough splits",
                    $"You do not have enough splits ({availableSplits.AvailableSplits} available) to {(boostModify is null ? "start" : "upgrade")} this boost ({neededSplits} required).\n Use `/boost inventory` to view your splits."));
            return;
        }

        string? invalidSplitAmount = null;
        if (factorSplits % splitsPrices.FactorSplitCost != 0)
            invalidSplitAmount = "factor splits must be a multiple of " + splitsPrices.FactorSplitCost;
        if (durationSplits % splitsPrices.DurationSplitCost != 0)
            invalidSplitAmount = "duration splits must be a multiple of " + splitsPrices.DurationSplitCost;
        if (cooldownSplits % splitsPrices.CooldownSplitCost != 0)
            invalidSplitAmount = "cooldown splits must be a multiple of " + splitsPrices.CooldownSplitCost;

        if (invalidSplitAmount is not null)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Invalid split amount", $"The {invalidSplitAmount}."));
            return;
        }

        var description = $"You have `{availableSplits.AvailableSplits}` splits available.\n" +
                          $"Using splits, you can power up your boost.\n" +
                          $"Use `/boost inventory` to learn more about your splits.\n_ _ \n" +
                          $"`🔥 Factor: +{splitsPrices.FactorSplitCost} splits => +{splitsPrices.FactorIncrease} factor`\n" +
                          $"`⌛ Duration: +{splitsPrices.DurationSplitCost} splits => +{splitsPrices.DurationIncreaseMinutes}min boost`\n" +
                          $"`💤 Cooldown: +{splitsPrices.CooldownSplitCost} splits => -{splitsPrices.CooldownIncreaseHours}h until next boost`\n";

        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle(boostModify is not null
                ? $"Upgrade your Dropboost from {Formatter.Timestamp(boostModify.StartDate.ToDateTimeOffset(), TimestampFormat.ShortDateTime)}"
                : "Start a new Dropboost")
            .WithDescription(description);

        var messageBuilder = new DiscordMessageBuilder()
            .AddEmbed(embedBuilder.Build());

        void UpdateComponents(string submitText, bool disable)
        {
            messageBuilder.ClearComponents();

            var minusFactor = new DiscordButtonComponent(DiscordButtonStyle.Secondary, "-fac", "-", disable);
            var plusFactor = new DiscordButtonComponent(DiscordButtonStyle.Secondary, "+fac", "+", disable);
            var labelFactor = new DiscordButtonComponent(DiscordButtonStyle.Primary, "fac",
                $"Boost Factor: {factorSplits} Splits (+{splitsPrices.DefaultFactor + factorSplits / splitsPrices.FactorSplitCost * splitsPrices.FactorIncrease:0.#}x)",
                true);

            var minusDur = new DiscordButtonComponent(DiscordButtonStyle.Secondary, "-dur", "-", disable);
            var plusDur = new DiscordButtonComponent(DiscordButtonStyle.Secondary, "+dur", "+", disable);
            var labelDur = new DiscordButtonComponent(DiscordButtonStyle.Primary, "dur",
                $"Boost Duration: {durationSplits} Splits (+{splitsPrices.DefaultDurationMinutes + durationSplits / splitsPrices.DurationSplitCost * splitsPrices.DurationIncreaseMinutes:0.#}min)",
                true);

            var minusCool = new DiscordButtonComponent(DiscordButtonStyle.Secondary, "-cool", "-", disable);
            var plusCool = new DiscordButtonComponent(DiscordButtonStyle.Secondary, "+cool", "+", disable);
            var labelCool = new DiscordButtonComponent(DiscordButtonStyle.Primary, "cool",
                $"Boost Cooldown: {cooldownSplits} Splits (-{splitsPrices.DefaultCooldownHours + cooldownSplits / splitsPrices.CooldownSplitCost * splitsPrices.CooldownIncreaseHours:0}h)",
                true);
            var start = new DiscordButtonComponent(DiscordButtonStyle.Success, "start",
                submitText + " (" + (cooldownSplits + durationSplits + factorSplits) + "/" +
                availableSplits.AvailableSplits + " Splits selected)", disable);

            messageBuilder
                .AddActionRowComponent([minusFactor, labelFactor, plusFactor])
                .AddActionRowComponent([minusDur, labelDur, plusDur])
                .AddActionRowComponent([minusCool, labelCool, plusCool])
                .AddActionRowComponent([start]);
        }

        UpdateComponents($"{(boostModify is null ? "Start" : "Upgrade")} Dropboost", false);
        await context.RespondAsync(messageBuilder);
        var response = await context.GetResponseAsync() ?? throw new Exception("no response but expected");
        var interactivity = context.Client.ServiceProvider.GetRequiredService<InteractivityExtension>();

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
        while (!buttonEvent.TimedOut)
        {
            await buttonEvent.Result.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
            var buttonId = buttonEvent.Result.Id;

            if (buttonId == "start")
            {
                await SubmitBoost();
                return;
            }

            uint[] oldSplits = [factorSplits, durationSplits, cooldownSplits];
            var mode = buttonId.StartsWith('+') ? 1 : -1;
            if (buttonId.Contains("fac")) factorSplits = (uint)(factorSplits + mode * splitsPrices.FactorSplitCost);
            if (buttonId.Contains("dur"))
                durationSplits = (uint)(durationSplits + mode * splitsPrices.DurationSplitCost);
            if (buttonId.Contains("cool"))
                cooldownSplits = (uint)(cooldownSplits + mode * splitsPrices.CooldownSplitCost);

            if (factorSplits + durationSplits + cooldownSplits > availableSplits.AvailableSplits)
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