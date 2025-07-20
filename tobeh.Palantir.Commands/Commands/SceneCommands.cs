using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using tobeh.Palantir.Commands.Checks;
using tobeh.Palantir.Commands.Extensions;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Commands.Commands;

/// <summary>
/// View, buy and use scenes
/// </summary>
/// <param name="logger"></param>
/// <param name="memberContext"></param>
/// <param name="scenesClient"></param>
/// <param name="inventoryClient"></param>
/// <param name="statsClient"></param>
/// <param name="eventsClient"></param>
[Command("scene")]
[TextAlias("sc")]
public class SceneCommands(
    ILogger<SceneCommands> logger,
    MemberContext memberContext,
    Scenes.ScenesClient scenesClient,
    Inventory.InventoryClient inventoryClient,
    Stats.StatsClient statsClient,
    Events.EventsClient eventsClient)
{
    /// <summary>
    /// View all scenes in your inventory
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="Exception"></exception>
    [Command("inventory"), TextAlias("inv"), RequirePalantirMember]
    public async Task ViewSceneInventory(CommandContext context)
    {
        logger.LogTrace("ViewSceneInventory(context)");

        var member = memberContext.Member;
        var inventory =
            await inventoryClient.GetSceneInventoryAsync(new GetSceneInventoryRequest { Login = member.Login });

        if (inventory.Scenes.Count == 0)
        {
            var emptyEmbed = new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithAuthor(
                    $"Viewing scenes 0 of 0 :(")
                .WithDescription("You don't own any scenes yet! " +
                                 $"Buy some with `/scene buy <id>` or visit {"the scenes listing".AsTypoLink("https://www.typo.rip/tools/scenes", "🧾")} to get inspired!")
                .WithTitle("Scene Inventory");

            emptyEmbed.AddField("Total worth:", "`🫧` 0 Bubbles");
            emptyEmbed.AddField("Event scenes:", "`🎟️` 0 Scenes collected");
            emptyEmbed.AddField("Uniqueness:",
                "`💎` Buy some scenes to get an uniqueness score!");

            await context.RespondAsync(emptyEmbed.Build());
            return;
        }

        // get all scenes, likely more performance than each individually
        var scenes = await scenesClient.GetAllScenes(new Empty()).ToListAsync();
        var sceneThemes = await scenesClient.GetAllSceneThemes(new Empty()).ToListAsync();
        var ranks = await scenesClient.GetSceneRanking(new Empty()).ToListAsync();

        var userScenes = scenes.Where(scene => inventory.Scenes.Any(inv => inv.SceneId == scene.Id)).Select(scene => new
        {
            Scene = scene,
            Themes = sceneThemes.Where(theme =>
                theme.SceneId == scene.Id &&
                inventory.Scenes.Any(inv => inv.SceneId == scene.Id && inv.SceneShift == theme.Shift)).ToList()
        }).ToList();
        var eventSceneCount = userScenes.Count(scene => scene.Scene.EventId > 0);
        var regularSceneCount = userScenes
            .Select(inv => inv.Scene.EventId == null && !inv.Scene.Exclusive ? 1 + inv.Themes.Count : 0).Sum();
        var priceInformation =
            await inventoryClient.GetScenePriceAsync(new ScenePriceRequest { BoughtSceneCount = regularSceneCount });

        var uniquenessMaxUsers = ranks.Max(rank => rank.TotalBought);
        var uniquenessUserScore = ranks
            .Where(rank => inventory.Scenes.Any(scene => scene.SceneId == rank.Id))
            .Select(rank => rank.TotalBought * 100 / uniquenessMaxUsers)
            .Average();

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor($"Viewing {userScenes.Select(scene => scene.Themes.Count + 1).Sum()} scenes")
            .WithTitle("Scene Inventory");

        embed.AddField("Total worth:", $"`🫧` {priceInformation.TotalBubblesSpent} Bubbles");
        embed.AddField("Next Scene Price:", $"`🫧` {priceInformation.NextPrice} Bubbles\n" +
                                            "With each scene you buy, the price of the next one increases.");
        embed.AddField("Event scenes:", $"`🎟️` {eventSceneCount} Scenes collected");
        embed.AddField("Uniqueness:",
            $"`💎` Your inventory has an uniqueness score of {100 - Math.Round(uniquenessUserScore)}%");

        if (inventory.Scenes.Count < 2)
        {
            embed.AddField("Command help:", "Use `/scene buy <id>` to buy a scene\n" +
                                            "Use `/scene use <id>` to wear a scene");
        }

        foreach (var fieldScenes in userScenes.Chunk(5))
        {
            embed.AddField("_ _",
                string.Join("\n", fieldScenes.Select(scene =>
                    $"`{scene.Scene.Id.AsTypoId()}` {scene.Scene.Name} {(scene.Themes.Count > 0 ? $" ({string.Join(", ", scene.Themes.Select(theme => theme.Name))})" : "")}")),
                true);
        }

        await context.RespondAsync(embed.Build());
    }

    /// <summary>
    /// View the most popular scenes
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="Exception"></exception>
    [Command("list")]
    public async Task ListScenes(CommandContext context)
    {
        logger.LogTrace("ListScenes(context)");

        var scenes = await scenesClient.GetAllScenes(new Empty()).ToListAsync();
        var sceneThemes = await scenesClient.GetAllSceneThemes(new Empty()).ToListAsync();
        var ranks = await scenesClient.GetSceneRanking(new Empty()).ToListAsync();

        var ranked = scenes
            .Select(scene => new { Scene = scene, Rank = ranks.FirstOrDefault(rank => rank.Id == scene.Id) })
            .OrderBy(rank => rank.Rank?.Rank ?? ranks.Count)
            .ToList();

        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithDescription("Scenes are ranked by their total purchases and active users.\n" +
                             $"View all scenes {"here".AsTypoLink("https://typo.rip/tools/scenes", "🌍")}")
            .WithTitle("Scene Ranking");

        foreach (var scene in ranked.Take(10))
        {
            var themeCount = sceneThemes.Count(theme => theme.SceneId == scene.Scene.Id);
            embedBuilder.AddField(
                $"**#{scene.Rank?.Rank}** {scene.Scene.Id.AsTypoId()} _ _ {scene.Scene.Name} {(themeCount > 0 ? $" (has themes)" : "")}",
                $"{scene.Rank?.TotalBought} bought, {scene.Rank?.ActiveUsers} active");
        }

        await context.RespondAsync(embedBuilder.Build());
    }

    /// <summary>
    /// View the details of a scene
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sceneId">The ID of the scene to show</param>
    /// <param name="shift">The theme shift, if a scene theme should be selected</param>
    /// <exception cref="Exception"></exception>
    [DefaultGroupCommand, Command("view"), TextAlias("vw")]
    public async Task ViewScene(CommandContext context, uint sceneId, uint? shift = null)
    {
        logger.LogTrace("ViewScene(context, {sceneId})", sceneId);

        var scene = await scenesClient.GetSceneByIdAsync(new GetSceneRequest { Id = (int)sceneId });
        var themes = await scenesClient.GetThemesOfScene(new GetSceneRequest { Id = scene.Id }).ToListAsync();

        var ranking = await scenesClient.GetSceneRanking(new Empty()).ToListAsync();
        var sceneRank = ranking.Find(s => s.Id == sceneId) ?? throw new Exception("Failed to calculate scene ranking");

        var theme = themes.FirstOrDefault(theme => theme.Shift == shift);
        if (theme is null && shift is { } shiftValue)
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Scene theme not found",
                $"A theme with shift {shiftValue} for scene {scene.Name} {scene.Id.AsTypoId()} was not found.\n" +
                $"You can see all available themes with `/scene view {scene.Id}`."));
            return;
        }

        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"{scene.Id.AsTypoId()} _ _ {theme?.Name ?? scene.Name}")
            .WithImageUrl(theme is not null
                ? $"https://static.typo.rip/sprites/rainbow/modulate.php?url={scene.Url}&hue={theme.Shift}"
                : scene.Url);

        if (scene.EventId is { } eventId)
        {
            var sceneEvent = await eventsClient.GetEventByIdAsync(new GetEventRequest { Id = eventId });
            var price = await scenesClient.GetEventSceneAsync(new GetEventSceneRequest { EventId = sceneEvent.Id });
            embedBuilder.AddField("Event Scene:", $"{sceneEvent.Name} Event {sceneEvent.Id.AsTypoId()}\n" +
                                                  $"Unlock this scene by collecting {price.Price} Bubbles during the event");
        }
        else
        {
            embedBuilder.AddField("Price:",
                $"`🫧` Check `/scene inventory` to see your current scene price.\nWith each scene you buy, the price of the next one increases.");
        }

        if (scene.Exclusive)
        {
            embedBuilder.AddField("Exclusive:", "`🔒` This scene is exclusive and can't be bought.");
        }

        if (themes.Count > 0 && shift == null)
        {
            var content = $"`🎨` This scene has color themes.\n" +
                          $"You can view a theme with the command `/scene view {scene.Id} <shiftId>`\n" +
                          string.Join("\n", themes.Select(theme => $"- {theme.Shift.AsTypoId()} {theme.Name}"));
            embedBuilder.AddField("Themes:", content);
        }

        if (theme is { } themeValue)
        {
            embedBuilder.AddField("Theme:", $"`🎨` This scene is a theme of the {scene.Name}.\n" +
                                            $"You can buy it with the command `/scene buy {scene.Id} {themeValue.Shift}`.");
        }

        embedBuilder.AddField("Artist:", $"`🖌️` Created by {scene.Artist ?? "tobeh"}");
        embedBuilder.AddField("Ranking:",
            $"`📈` #{sceneRank.Rank}: {sceneRank.TotalBought} bought, {sceneRank.ActiveUsers} active \n" +
            $"View all scenes {"here".AsTypoLink("https://typo.rip/tools/scenes", "🌍")}");

        await context.RespondAsync(embedBuilder.Build());
    }

    /// <summary>
    /// Buy a new scene
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sceneId">The ID of the scene that will be added to your inventory</param>
    /// <param name="shift">The theme shift, if a scene theme should be selected</param>
    [Command("buy"), RequirePalantirMember]
    public async Task BuyScene(CommandContext context, uint sceneId, uint? shift = null)
    {
        logger.LogTrace("BuyScene(context, {sceneId})", sceneId);

        var scene = await scenesClient.GetSceneByIdAsync(new GetSceneRequest { Id = (int)sceneId });

        var member = memberContext.Member;
        var inventory =
            await inventoryClient.GetSceneInventoryAsync(new GetSceneInventoryRequest { Login = member.Login });
        var sceneThemes = await scenesClient.GetThemesOfScene(new GetSceneRequest { Id = scene.Id }).ToListAsync();
        var sceneTheme = sceneThemes.FirstOrDefault(theme => theme.Shift == shift);

        // check if theme exists
        if (shift is { } shiftValue && sceneTheme is null)
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Scene theme not found",
                $"A theme with shift {shiftValue} for scene {scene.Name} {scene.Id.AsTypoId()} was not found.\n" +
                $"You can see all available themes with `/scene view {scene.Id}`."));
            return;
        }

        // check if the user has bought this scene already
        if (inventory.Scenes.Any(inv => inv.SceneId == sceneId && inv.SceneShift == shift))
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Scene already bought",
                sceneTheme is { } theme
                    ? $"You already own the theme {theme.Name} of the scene {scene.Name} {scene.Id.AsTypoId()}. You can use it with `/scene use {scene.Id} {theme.Shift}`."
                    : $"You already own the scene {scene.Name} {scene.Id.AsTypoId()}. You can use it with `/scene use {scene.Id}`."));
            return;
        }

        // if theme, check if user owns base scene
        if (sceneTheme is not null &&
            !inventory.Scenes.Any(inv => inv.SceneId == sceneTheme.SceneId && inv.SceneShift is null))
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Scene not in inventory",
                    $"Before you can use the theme {sceneTheme.Name} of scene {scene.Name} {scene.Id.AsTypoId()}, you need to own the regular scene.\n" +
                    $"You can buy the scene with `/scene buy {sceneTheme.SceneId}`."));
            return;
        }

        // check if scene is exclusive
        if (scene.Exclusive)
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context, "Scene is exclusive",
                $"The scene {scene.Name} {scene.Id.AsTypoId()} is exclusive and can't be bought."));
            return;
        }

        // check if the credit of the user is sufficient during event
        if (scene.EventId is { } eventId)
        {
            var sceneEvent = await eventsClient.GetEventByIdAsync(new GetEventRequest { Id = eventId });
            var traceStartDate = Timestamp.FromDateTimeOffset(sceneEvent.StartDate.ToDateTimeOffset().AddDays(-1));
            var bubbleRange = await statsClient.GetBubbleTimespanRangeAsync(new BubbleTimespanRangeRequest
                { Login = member.Login, StartDate = traceStartDate, EndDate = sceneEvent.EndDate });
            var bubblesCollected = bubbleRange.EndAmount - bubbleRange.StartAmount;
            var eventScenePrice =
                await scenesClient.GetEventSceneAsync(new GetEventSceneRequest { EventId = sceneEvent.Id });

            if (bubblesCollected < eventScenePrice.Price)
            {
                if (sceneEvent.EndDate.ToDateTimeOffset() < DateTimeOffset.UtcNow)
                {
                    await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                        "Event ended",
                        $"The {sceneEvent.Name} event has ended, so you can no longer collect bubbles to buy the scene {scene.Name} {scene.Id.AsTypoId()}.\n" +
                        $"FYI: you collected {bubblesCollected} out of {eventScenePrice.Price} required Bubbles during the event!"));
                    return;
                }

                await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                    "Too few bubbles collected",
                    $"You need to collect {eventScenePrice.Price} Bubbles during the {sceneEvent.Name} event to buy {scene.Name} {scene.Id.AsTypoId()}.\n" +
                    $"So far, you have {bubblesCollected} Bubbles collected.\n" +
                    $"Play a few more hours on skribbl and check `/event view {scene.EventId}` to see your progress."));
                return;
            }
        }
        else
        {
            var bubbleCredit =
                await inventoryClient.GetBubbleCreditAsync(new GetBubbleCreditRequest { Login = member.Login });
            var scenes = await scenesClient.GetAllScenes(new Empty()).ToListAsync();
            var userScenes = scenes.Where(scene => inventory.Scenes.Any(inv => inv.SceneId == scene.Id)).Select(scene =>
                new
                {
                    Scene = scene,
                    Themes = sceneThemes.Where(theme =>
                        theme.SceneId == scene.Id &&
                        inventory.Scenes.Any(inv => inv.SceneId == scene.Id && inv.SceneShift == theme.Shift)).ToList()
                }).ToList();

            var regularSceneCount = userScenes
                .Select(inv => inv.Scene.EventId == null && !inv.Scene.Exclusive ? 1 + inv.Themes.Count : 0).Sum();
            var regularScenePrice = await inventoryClient.GetScenePriceAsync(new ScenePriceRequest
                { BoughtSceneCount = regularSceneCount });
            if (bubbleCredit.AvailableCredit < regularScenePrice.NextPrice)
            {
                await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                    "Bubble credit too low",
                    $"You need {regularScenePrice.NextPrice} Bubbles to buy your next scene, but you only have {bubbleCredit.AvailableCredit} available.\n" +
                    $"Collect more Bubbles by playing skribbl or catch drops to get bonus bubbles!\nYou can see your bubble credit with `/inventory`."));
                return;
            }
        }

        // buy scene
        await inventoryClient.BuySceneAsync(new BuySceneRequest
            { Login = member.Login, SceneId = scene.Id, SceneShift = sceneTheme?.Shift });
        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor("You unlocked a new scene!")
            .WithTitle($"{scene.Id.AsTypoId()} _ _ {sceneTheme?.Name ?? scene.Name}")
            .WithImageUrl(sceneTheme is not null
                ? $"https://static.typo.rip/sprites/rainbow/modulate.php?url={scene.Url}&hue={sceneTheme.Shift}"
                : scene.Url);

        embedBuilder.AddField("Use it:",
            $"`🌄` Use the command `/scene use {scene.Id}{(sceneTheme is not null ? $" {sceneTheme.Shift}" : "")}` to use this scene as your skribbl avatar background.");

        await context.RespondAsync(embedBuilder.Build());
    }

    /// <summary>
    /// Choose a scene as your avatar background on skribbl.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sceneId">The ID of a scene, or empty/0 to choose no scene</param>
    /// <param name="shift">The theme shift, if a scene theme should be selected</param>
    [Command("use"), RequirePalantirMember]
    public async Task UseScene(CommandContext context, uint? sceneId = null, uint? shift = null)
    {
        logger.LogTrace("UseScene(context, {sceneId})", sceneId);

        if (sceneId is null or 0)
        {
            sceneId = null;
            shift = null;
        }

        var scene = sceneId is { } sceneIdValue
            ? await scenesClient.GetSceneByIdAsync(new GetSceneRequest { Id = (int)sceneIdValue })
            : null;
        var sceneThemes = scene is not null
            ? await scenesClient.GetThemesOfScene(new GetSceneRequest { Id = scene.Id }).ToListAsync()
            : null;
        var theme = sceneThemes?.FirstOrDefault(theme => theme.Shift == shift);

        if (scene is not null && shift is { } shiftValue && theme is null)
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Scene theme not found",
                $"A theme with shift {shiftValue} for scene {scene.Name} {scene.Id.AsTypoId()} was not found.\n" +
                $"You can see all available themes with `/scene view {scene.Id}`."));
            return;
        }

        var member = memberContext.Member;
        var inventory =
            await inventoryClient.GetSceneInventoryAsync(new GetSceneInventoryRequest { Login = member.Login });

        // check if the user owns this scene
        if (scene is not null && !inventory.Scenes.Any(inv => inv.SceneId == scene.Id && inv.SceneShift == shift))
        {
            if (theme is not null)
            {
                await context.RespondAsync(new DiscordEmbedBuilder()
                    .WithPalantirErrorPresets(context, "Scene not in inventory",
                        $"You don't own the scene {scene.Name} {scene.Id.AsTypoId()} with theme {theme.Name} yet.\n" +
                        $"You can buy the theme with `/scene buy {sceneId} {theme.Shift}`."));
            }
            else
            {
                await context.RespondAsync(new DiscordEmbedBuilder()
                    .WithPalantirErrorPresets(context, "Scene not in inventory",
                        $"You don't own the scene {scene.Name} {scene.Id.AsTypoId()} yet.\n" +
                        $"You can buy the it with `/scene buy {sceneId}`."));
            }

            return;
        }

        // activate new scene
        await inventoryClient.UseSceneAsync(new UseSceneRequest
            { Login = member.Login, SceneId = scene?.Id, SceneShift = theme?.Shift });

        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor(scene is null ? "You removed your avatar background." : "You chose your avatar background!")
            .WithTitle(scene is null ? "Such empty 💨" : $"{scene.Id.AsTypoId()} _ _ {theme?.Name ?? scene.Name}")
            .WithImageUrl(theme is not null
                ? $"https://static.typo.rip/sprites/rainbow/modulate.php?url={scene!.Url}&hue={theme.Shift}"
                : scene?.Url ?? "");

        if (scene is not null)
            embedBuilder.WithDescription(
                $"The scene {scene.Name} will now be displayed behind your avatar in skribbl lobbies.\n" +
                $"To remove it, use the command `/scene use`.");
        else embedBuilder.WithDescription("Your avatar background has been cleared.");

        await context.RespondAsync(embedBuilder.Build());
    }
}