using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands.Attributes;
using DSharpPlus.Commands.Trees.Attributes;
using DSharpPlus.Entities;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Palantir_Commands.Discord.Checks;
using Palantir_Commands.Discord.Extensions;
using Palantir_Commands.Services;
using Valmar;
using Valmar_Client.Grpc;

namespace Palantir_Commands.Discord.Commands;

[Command("outfit"), TextAlias("of")]
public class OutfitCommands(
    ILogger<OutfitCommands> logger,
    Outfits.OutfitsClient outfitsClient,
    MemberContext memberContext,
    Inventory.InventoryClient inventoryClient,
    Sprites.SpritesClient spritesClient,
    Scenes.ScenesClient scenesClient
)
{
    
    /// <summary>
    /// Lists all saved outfits
    /// </summary>
    /// <param name="context"></param>
    [Command("list"), DefaultGroupCommand, RequirePalantirMember(MemberFlagMessage.Beta)]
    public async Task ListOutfits(CommandContext context)
    {
        logger.LogTrace("ListOutfits(context)");

        var member = memberContext.Member;
        var outfits = await outfitsClient.GetOutfits(new GetOutfitsRequest { Login = member.Login }).ToListAsync();

        const int batchSize = 12;
        var pages = outfits.Batch(batchSize).Select((batch, idx) => new
        {
            Page = idx + 1,
            Outfits = batch.ToList()
        }).Select(page =>
        {
            var embed = new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithTitle("User Outfits")
                .WithDescription(
                    "Outfits are a setting of sprite combo, sprite colors and a scene which you can save for quick access.");

            if (page.Outfits.Count < 3)
            {
                embed.AddField("Command help:",
                    "Use `/outfit save (name)` to save your current appearance as a new outfit.\n" +
                    "Use `/outfit use (name)` to activate one of your saved outfits.\n" +
                    "Use `/outfit delete (name)` to remove an outfit.\n"
                );
            }

            page.Outfits.ForEach(outfit =>
            {
                var colorCount = outfit.SpriteSlotConfiguration.Count(slot => slot.ColorShift is not null);

                embed.AddField($"`🧑‍🎨` {outfit.Name}",
                    $"```md\n- Colors: {colorCount} set\n- Scene: {outfit.SceneId?.ToString() ?? "none"}\n- Combo: {outfit.SpriteSlotConfiguration.Count} slots\n```",
                    true);
            });

            return embed;
        }).ToList();

        await context.RespondPalantirPaginationAsync(pages);
    }

    [Command("save"), TextAlias("sv"), RequirePalantirMember(MemberFlagMessage.Beta)]
    public async Task SaveOutfit(CommandContext context, string name)
    {
        logger.LogTrace("SaveOutfit(context, name={name})", name);
        
        var member = memberContext.Member;
        var spriteInv = await inventoryClient.GetSpriteInventory(new GetSpriteInventoryRequest { Login = member.Login })
            .ToListAsync();
        var sceneInv = await inventoryClient.GetSceneInventoryAsync(new GetSceneInventoryRequest { Login = member.Login });

        var outfit = new OutfitMessage
        {
            Name = name,
            SceneId = sceneInv.ActiveId,
            SpriteSlotConfiguration = { spriteInv.Where(slot => slot.Slot > 0) }
        };
        
        await outfitsClient.SaveOutfitAsync(new SaveOutfitRequest { Login = member.Login, Outfit = outfit });
        
        await context.RespondAsync(embed: new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor("What a drip")
            .WithTitle($"Outfit `{name}` saved")
            .WithDescription("This outfit contains your current sprites and scene.\n You can now use this outfit with `/outfit use (name)` or view details with `/outfit view (name)`"));
    }
    
    [Command("delete"), TextAlias("dl"), RequirePalantirMember(MemberFlagMessage.Beta)]
    public async Task DeleteOutfit(CommandContext context, string name)
    {
        logger.LogTrace("DeleteOutfit(context, name={name})", name);
        
        var member = memberContext.Member;

        try
        {
            await outfitsClient.GetOutfitAsync(new GetOutfitRequest { Login = member.Login, OutfitName = name });
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        {
            await context.RespondAsync(embed: new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, $"Outfit `{name}` not found",
                    "Use `/outfit list` to see all your saved outfits."));
            return;
        }
        
        await outfitsClient.DeleteOutfitAsync(new DeleteOutfitRequest { Login = member.Login, OutfitName = name });
        
        await context.RespondAsync(embed: new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"Outfit `{name}` deleted")
            .WithDescription("To save a new outfit with your current style, use `/outfit save (name)`"));
    }
    
    [Command("use"), RequirePalantirMember(MemberFlagMessage.Beta)]
    public async Task UseOutfit(CommandContext context, string name)
    {
        logger.LogTrace("UseOutfit(context, name={name})", name);
        
        var member = memberContext.Member;

        try
        {
            await outfitsClient.GetOutfitAsync(new GetOutfitRequest { Login = member.Login, OutfitName = name });
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        {
            await context.RespondAsync(embed: new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, $"Outfit `{name}` not found",
                    "Use `/outfit list` to see all your saved outfits."));
            return;
        }
        
        await outfitsClient.UseOutfitAsync(new UseOutfitRequest { Login = member.Login, OutfitName = name });
        
        await context.RespondAsync(embed: new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"Outfit `{name}` set")
            .WithDescription("Sprites and scenes from this outfit have been loaded!"));
    }
    
    [Command("view"), TextAlias("vw"), RequirePalantirMember(MemberFlagMessage.Beta)]
    public async Task ViewOutfit(CommandContext context, string name)
    {
        logger.LogTrace("ViewOutfit(context, name={name})", name);
        
        var member = memberContext.Member;
        OutfitMessage outfit;
        try
        {
            outfit = await outfitsClient.GetOutfitAsync(new GetOutfitRequest { Login = member.Login, OutfitName = name });
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        {
            await context.RespondAsync(embed: new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, $"Outfit `{name}` not found",
                    "Use `/outfit list` to see all your saved outfits."));
            return;
        }
        
        var sprites = await spritesClient.GetAllSprites(new Empty()).ToListAsync();
        var scene = outfit.SceneId is {} sceneValue ? await scenesClient.GetSceneByIdAsync(new GetSceneRequest { Id = sceneValue }) : null;
        
        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor("Viewing an user outfit")
            .WithTitle(name)
            .WithDescription($"You can use this outfit with `/outfit use {name}`. \n" +
                             $"To update it to your current style, use `/outfit save {name}`.\n" +
                             $"To delete the outfit, use `/outfit delete {name}`.");

        var combo = string.Join("\n", outfit.SpriteSlotConfiguration.Select(slot =>
        {
            var sprite = sprites.First(spt => spt.Id == slot.SpriteId);
            var shift = slot.ColorShift is not null ? $"(color shift: {slot.ColorShift})" : "";
            return $"{sprite.Id.AsTypoId()}  {sprite.Name}  {shift}";
        }));
        embed.AddField("Combo:", combo.Length == 0 ? "Empty" : combo);
        embed.AddField("Scene:", scene is null ? "None" : $"{scene.Id.AsTypoId()} {scene.Name}");
        
        await context.RespondAsync(embed: embed);
    }
}