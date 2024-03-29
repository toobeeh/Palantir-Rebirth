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

[Command("scene")]
[TextAlias("sc")]
[RequirePalantirMember(MemberFlagMessage.Beta)]
public class SceneCommands(
    ILogger<SceneCommands> logger, 
    Scenes.ScenesClient scenesClient, 
    Inventory.InventoryClient inventoryClient,
    Members.MembersClient membersClient,
    Stats.StatsClient statsClient,
    Events.EventsClient eventsClient)
{
    
    /// <summary>
    /// View all scenes in your inventory
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="Exception"></exception>
    [Command("inventory")]
    [TextAlias("inv")]
    public async Task ViewSceneInventory(CommandContext context)
    {
        logger.LogTrace("ViewSceneInventory(context)");
        
        var member = await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest {Id = (long)context.User.Id});
        var inventory = await inventoryClient.GetSceneInventoryAsync(new GetSceneInventoryRequest { Login = member.Login });
        
        // get all scenes, likely more performance than each individually
        var scenes = await scenesClient.GetAllScenes(new Empty()).ToListAsync();
        var ranks = await scenesClient.GetSceneRanking(new Empty()).ToListAsync();

        var userScenes = scenes.Where(scene => inventory.SceneIds.Contains(scene.Id)).ToList();
        var eventSceneCount = userScenes.Count(scene => scene.EventId > 0);
        var priceInformation = await inventoryClient.GetScenePriceAsync(new ScenePriceRequest
            { BoughtSceneCount = userScenes.Count(scene => !scene.Exclusive && scene.EventId is null) });

        var uniquenessMaxUsers = ranks.Max(rank => rank.TotalBought);
        var uniquenessUserScore = ranks
            .Where(rank => inventory.SceneIds.Contains(rank.Id))
            .Select(rank => rank.TotalBought * 100 / uniquenessMaxUsers)
            .Average();
        
        
        var embed =  new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor($"Viewing {inventory.SceneIds.Count} scenes")
            .WithTitle("Scene Inventory");
        
        embed.AddField("Total worth:", $"`🫧` {priceInformation.TotalBubblesSpent} Bubbles");
        embed.AddField("Next Scene Price:", $"`🫧` {priceInformation.NextPrice} Bubbles\n" +
                                            "With each scene you buy, the price of the next one increases.");
        embed.AddField("Event scenes:", $"`🎟️` {eventSceneCount} Scenes collected");
        embed.AddField("Uniqueness:", $"`💎` Your inventory has an uniqueness score of {100 - Math.Round(uniquenessUserScore)}%");

        if (inventory.SceneIds.Count < 2)
        {
            embed.AddField("Command help:", "Use `/scene buy (id)` to buy a scene\n" +
                                            "Use `/scene use (id)` to wear a scene");
        }
        
        foreach (var fieldScenes in userScenes.Batch(5))
        {
            embed.AddField("_ _", string.Join("\n", fieldScenes.Select(scene => $"`{scene.Id.AsTypoId()}` {scene.Name}")), true);
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
            embedBuilder.AddField($"**#{scene.Rank?.Rank}** {scene.Scene.Id.AsTypoId()} _ _ {scene.Scene.Name}", $"{scene.Rank?.TotalBought} bought, {scene.Rank?.ActiveUsers} active");
        }

        await context.RespondAsync(embedBuilder.Build());
    }
    
    /// <summary>
    /// View the details of a scene
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sceneId">The ID of the scene to show</param>
    /// <exception cref="Exception"></exception>
    [DefaultGroupCommand]
    [Command("view")]
    public async Task ViewScene(CommandContext context, int sceneId)
    {
        logger.LogTrace("ViewScene(context, {sceneId})", sceneId);
        
        var scene = await scenesClient.GetSceneByIdAsync(new GetSceneRequest { Id = sceneId });
        
        var ranking = await scenesClient.GetSceneRanking(new Empty()).ToListAsync();
        var sceneRank = ranking.Find(s => s.Id == sceneId) ?? throw new Exception("Failed to calculate scene ranking");
        
        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"{scene.Id.AsTypoId()} _ _ {scene.Name}")
            .WithImageUrl(scene.Url);

        if (scene.EventId is { } eventId)
        {
            var sceneEvent = await eventsClient.GetEventByIdAsync(new GetEventRequest { Id = eventId });
            var price = await scenesClient.GetEventScenePriceAsync(new GetEventScenePriceRequest { EventDayLength = sceneEvent.Length });
            embedBuilder.AddField("Event Scene:", $"{sceneEvent.Name} Event {sceneEvent.Id.AsTypoId()}\n" +
                                                   $"Unlock this scene by collecting {price.Price} Bubbles during the event");
        }
        else
        {
            embedBuilder.AddField("Price:", $"`🫧` Check `/scene inventory` to see your current scene price.\nWith each scene you buy, the price of the next one increases.");
        }
        
        if(scene.Exclusive)
        {
            embedBuilder.AddField("Exclusive:", "`🔒` This scene is exclusive and can't be bought.");
        }
        
        embedBuilder.AddField("Artist:", $"`🖌️` Created by {scene.Artist ?? "tobeh"}");
        embedBuilder.AddField("Ranking:", $"`📈` #{sceneRank.Rank}: {sceneRank.TotalBought} bought, {sceneRank.ActiveUsers} active \n" +
                                          $"View all scenes {"here".AsTypoLink("https://typo.rip/tools/scenes", "🌍")}");
        
        await context.RespondAsync(embedBuilder.Build());
    }
    
    [Command("buy")]
    public async Task BuyScene(CommandContext context, int sceneId)
    {
        logger.LogTrace("BuyScene(context, {sceneId})", sceneId);
        
        var scene = await scenesClient.GetSceneByIdAsync(new GetSceneRequest { Id = sceneId });
        
        // check if the user has bought this scene already
        var user = await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest {Id = (long)context.User.Id});
        var inventory = await inventoryClient.GetSceneInventoryAsync(new GetSceneInventoryRequest { Login = user.Login });
        if(inventory.SceneIds.Contains(scene.Id))
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context, "Scene already bought", $"You already own {scene.Name} {scene.Id.AsTypoId()}. You can use it with `/scene use {scene.Id}`."));
            return;
        }
        
        // check if scene is exclusive
        if(scene.Exclusive)
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context, "Scene is exclusive", $"The scene {scene.Name} {scene.Id.AsTypoId()} is exclusive and can't be bought."));
            return;
        }

        // check if the credit of the user is sufficient
        if (scene.EventId is { } eventId)
        {
            var sceneEvent = await eventsClient.GetEventByIdAsync(new GetEventRequest { Id = eventId });
            var traceStartDate = Timestamp.FromDateTimeOffset(sceneEvent.StartDate.ToDateTimeOffset().AddDays(-1));
            var bubbleRange = await statsClient.GetBubbleTimespanRangeAsync(new BubbleTimespanRangeRequest { Login = user.Login, StartDate = traceStartDate, EndDate = sceneEvent.EndDate });
            var bubblesCollected = bubbleRange.EndAmount - bubbleRange.StartAmount;
            var eventScenePrice = await scenesClient.GetEventScenePriceAsync(new GetEventScenePriceRequest
                { EventDayLength = sceneEvent.Length });
            
            if(bubblesCollected < eventScenePrice.Price) 
            {
                await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context, "Too few bubbles collected", $"You need to collect {eventScenePrice.Price} Bubbles during the {sceneEvent.Name} event to buy {scene.Name} {scene.Id.AsTypoId()}.\n" +
                    $"So far, you have {bubblesCollected} Bubbles collected.\n" +
                    $"Play a few more hours on skribbl and check `/event` to see your progress."));
                return;
            }
        }   
        else
        {   
            var bubbleCredit = await inventoryClient.GetBubbleCreditAsync(new GetBubbleCreditRequest { Login = user.Login });
            var scenes = await scenesClient.GetAllScenes(new Empty()).ToListAsync();
            var regularSceneCount = scenes.Count(scene => inventory.SceneIds.Contains(scene.Id) && !scene.Exclusive && scene.EventId is null);
            var regularScenePrice = await inventoryClient.GetScenePriceAsync(new ScenePriceRequest
                { BoughtSceneCount = regularSceneCount });
            if(bubbleCredit.AvailableCredit < regularScenePrice.NextPrice) 
            {
                await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context, "Bubble credit too low", $"You need {regularScenePrice.NextPrice} Bubbles to buy your next scene, but you only have {bubbleCredit.AvailableCredit} available.\n" +
                    $"Collect more Bubbles by playing skribbl or catch drops to get bonus bubbles!"));
                return;
            }
        }
        
        // buy scene
        await inventoryClient.BuySceneAsync(new BuySceneRequest { Login = user.Login, SceneId = scene.Id });
        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor("You unlocked a new scene!")
            .WithTitle($"{scene.Id.AsTypoId()} _ _ {scene.Name}")
            .WithImageUrl(scene.Url);
        
        embedBuilder.AddField("Use it:", $"`🌄` Use the command `/scene use {scene.Id}` to use this scene as your skribbl avatar background.");
        
        await context.RespondAsync(embedBuilder.Build());
    }
    
    /// <summary>
    /// Choose a scene as your avatar background on skribbl.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sceneId">The ID of a scene, or none to remove it</param>
    [Command("use")]
    public async Task UseScene(CommandContext context, int? sceneId = null)
    {
        logger.LogTrace("UseScene(context, {sceneId})", sceneId);
        
        var scene = sceneId is {} sceneIdValue ?await scenesClient.GetSceneByIdAsync(new GetSceneRequest { Id = sceneIdValue }) : null;
        var user = await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest {Id = (long)context.User.Id});
        var inventory = await inventoryClient.GetSceneInventoryAsync(new GetSceneInventoryRequest { Login = user.Login });
        
        // check if the user owns this scene
        if(scene is not null && !inventory.SceneIds.Contains(scene.Id))
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Scene not in inventory", $"You don't own the scene {scene.Name} {scene.Id.AsTypoId()} yet.\n" +
                                                                              $"You can buy it with `/scene buy {sceneId}`."));
            return;
        }
        
        // activate new scene
        await inventoryClient.UseSceneAsync(new UseSceneRequest { Login = user.Login, SceneId = scene?.Id });
        
        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor(scene is null ? "You removed your avatar background." : "You chose your avatar background!")
            .WithTitle(scene is null ? "Such empty 💨" : $"{scene.Id.AsTypoId()} _ _ {scene.Name}")
            .WithImageUrl(scene?.Url ?? "");
        
        if(scene is not null) embedBuilder.WithDescription($"This scene {scene.Name} will now be displayed behind your avatar in skribbl lobbies.\n" +
                                     $"To remove it, use the command `/scene use`.");
        else embedBuilder.WithDescription("Your avatar background has been cleared.");
        
        await context.RespondAsync(embedBuilder.Build());
    }
}