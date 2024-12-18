using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using tobeh.Palantir.Commands.Checks;
using tobeh.Palantir.Commands.Extensions;
using tobeh.TypoContentService;
using tobeh.TypoContentService.Client.Util;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Commands.Commands;

/// <summary>
/// Create and use outfits for quick switching between sprites and scenes
/// </summary>
/// <param name="logger"></param>
/// <param name="outfitsClient"></param>
/// <param name="imageGeneratorClient"></param>
/// <param name="memberContext"></param>
/// <param name="inventoryClient"></param>
/// <param name="spritesClient"></param>
/// <param name="scenesClient"></param>
[Command("outfit"), TextAlias("of")]
public class OutfitCommands(
    ILogger<OutfitCommands> logger,
    Outfits.OutfitsClient outfitsClient,
    ImageGenerator.ImageGeneratorClient imageGeneratorClient,
    MemberContext memberContext,
    Inventory.InventoryClient inventoryClient,
    Sprites.SpritesClient spritesClient,
    Scenes.ScenesClient scenesClient
)
{
    /// <summary>
    /// List all saved outfits
    /// </summary>
    /// <param name="context"></param>
    [Command("list"), DefaultGroupCommand, RequirePalantirMember]
    public async Task ListOutfits(CommandContext context)
    {
        logger.LogTrace("ListOutfits(context)");

        var member = memberContext.Member;
        var outfits = await outfitsClient.GetOutfits(new GetOutfitsRequest { Login = member.Login }).ToListAsync();

        const int batchSize = 12;
        var pages = outfits.Chunk(batchSize).Select((batch, idx) => new
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
                    "Use `/outfit save <name>` to save your current appearance as a new outfit.\n" +
                    "Use `/outfit use <name>` to activate one of your saved outfits.\n" +
                    "Use `/outfit delete <name>` to remove an outfit.\n"
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

    /// <summary>
    /// Save your current scene, sprites and color customization as a new outfit
    /// </summary>
    /// <param name="context"></param>
    /// <param name="name">The new outfit name, or an existing outfit that will be overwritten</param>
    [Command("save"), TextAlias("sv"), RequirePalantirMember]
    public async Task SaveOutfit(CommandContext context, string name)
    {
        logger.LogTrace("SaveOutfit(context, name={name})", name);

        var member = memberContext.Member;
        var spriteInv = await inventoryClient.GetSpriteInventory(new GetSpriteInventoryRequest { Login = member.Login })
            .ToListAsync();
        var sceneInv =
            await inventoryClient.GetSceneInventoryAsync(new GetSceneInventoryRequest { Login = member.Login });

        var outfit = new OutfitMessage
        {
            Name = name,
            SceneId = sceneInv.ActiveId,
            SceneShift = sceneInv.ActiveShift,
            SpriteSlotConfiguration = { spriteInv.Where(slot => slot.Slot > 0) }
        };

        await outfitsClient.SaveOutfitAsync(new SaveOutfitRequest { Login = member.Login, Outfit = outfit });

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor("What a drip")
            .WithTitle($"Outfit `{name}` saved")
            .WithDescription(
                "This outfit contains your current sprites and scene.\n You can now use this outfit with `/outfit use <name>` or view details with `/outfit view <name>`");

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
    /// Show a preview of your current skribbl outfit
    /// </summary>
    /// <param name="context"></param>
    [Command("preview"), TextAlias("pv"), RequirePalantirMember]
    public async Task PreviewOutfit(CommandContext context)
    {
        logger.LogTrace("PreviewOutfit(context)");

        var member = memberContext.Member;
        var spriteInv = await inventoryClient.GetSpriteInventory(new GetSpriteInventoryRequest { Login = member.Login })
            .ToListAsync();
        var sceneInv =
            await inventoryClient.GetSceneInventoryAsync(new GetSceneInventoryRequest { Login = member.Login });

        SceneReply? scene = null;
        SceneThemeReply? theme = null;
        if (sceneInv.ActiveId is { } sceneIdVal)
        {
            var themes = await scenesClient.GetThemesOfScene(new GetSceneRequest { Id = sceneIdVal })
                .ToDictionaryAsync(scene => scene.Shift);
            theme = sceneInv.ActiveShift is { } sceneShiftVal ? themes[sceneShiftVal] : null;
            scene = await scenesClient.GetSceneByIdAsync(new GetSceneRequest { Id = sceneIdVal });
        }

        var sprites = await spritesClient.GetAllSprites(new Empty()).ToListAsync();

        var combo = string.Join("\n", spriteInv.Where(s => s.Slot > 0).OrderBy(s => s.Slot).Select(slot =>
        {
            var sprite = sprites.First(spt => spt.Id == slot.SpriteId);
            var shift = slot.ColorShift is not null ? $"(color shift: {slot.ColorShift})" : "";
            return $"{sprite.Id.AsTypoId()}  {sprite.Name}  {shift}";
        }));

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor("Viewing your current outfit")
            .WithTitle("Outfit Preview")
            .AddField("Combo", $"{(combo.Length == 0 ? "Empty" : combo)}")
            .AddField("Scene",
                $"{(scene is null ? "None" : $"{scene.Id.AsTypoId()} {scene.Name} {(theme is not null ? $"(Theme: {theme.Name})" : "")}")}")
            .WithDescription($"You can save this outfit with `/outfit save <name>`.");

        var colorMaps = spriteInv
            .Where(spt => spt.ColorShift != null && spt.Slot > 0)
            .Select(slot => new ColorMapMessage { HueShift = slot.ColorShift ?? 100, SpriteId = slot.SpriteId });

        var comboIds = spriteInv
            .Where(slot => slot.Slot > 0).OrderBy(slot => slot.Slot)
            .Select(slot => slot.SpriteId);
        var imageFile = await imageGeneratorClient.GenerateSpriteCombo(new GenerateComboMessage
            { SpriteIds = { comboIds }, ColorMaps = { colorMaps } }).CollectFileChunksAsync();

        await context.RespondAsync(
            embed.ToMessageBuilderWithAttachmentImage(imageFile.FileName, imageFile.Data));
    }

    /// <summary>
    /// Delete an outfit from your outfit list
    /// </summary>
    /// <param name="context"></param>
    /// <param name="name">The name of the outfit that will be removed</param>
    [Command("delete"), TextAlias("dl"), RequirePalantirMember]
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
            .WithDescription("To save a new outfit with your current style, use `/outfit save <name>`"));
    }

    /// <summary>
    /// Load sprites, colors and scene from an outfit
    /// </summary>
    /// <param name="context"></param>
    /// <param name="name">The name of the outfit that will be loaded</param>
    [Command("use"), RequirePalantirMember]
    public async Task UseOutfit(CommandContext context, string name)
    {
        logger.LogTrace("UseOutfit(context, name={name})", name);

        var member = memberContext.Member;

        OutfitMessage outfit;
        try
        {
            outfit = await outfitsClient.GetOutfitAsync(
                new GetOutfitRequest { Login = member.Login, OutfitName = name });
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        {
            await context.RespondAsync(embed: new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, $"Outfit `{name}` not found",
                    "Use `/outfit list` to see all your saved outfits."));
            return;
        }

        if (!member.MappedFlags.Any(flag => flag is MemberFlagMessage.Patron or MemberFlagMessage.Admin) &&
            outfit.SpriteSlotConfiguration.Count(slot => slot.ColorShift != null) > 1)
        {
            await context.RespondAsync(embed: new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, $"Outfit `{name}` is too powerful",
                    $"This outfit has more than one rainbow sprite configured.\nTo use this outfit, you need a {"Patreon".AsTypoLink("https://www.patreon.com/skribbltypo", "🩵")} subscription."));
            return;
        }

        var memberSlots =
            await inventoryClient.GetSpriteSlotCountAsync(new GetSpriteSlotCountRequest { Login = member.Login });
        var tooManySlots = memberSlots.UnlockedSlots < outfit.SpriteSlotConfiguration.Count;
        if (tooManySlots)
        {
            foreach (var slot in outfit.SpriteSlotConfiguration.ToList()
                         .Where(slot => slot.Slot > memberSlots.UnlockedSlots))
            {
                outfit.SpriteSlotConfiguration.Remove(slot);
            }
            /*await context.RespondAsync(embed: new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, $"Outfit `{name}` is too powerful",
                    $"This outfit has more too many sprite slots configured.\nCurrently, you have {memberSlots.UnlockedSlots} slots available."));
            return;*/
        }

        await outfitsClient.UseOutfitAsync(new UseOutfitRequest { Login = member.Login, OutfitName = name });

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"Outfit `{name}` set")
            .WithDescription("Sprites and scenes from this outfit have been loaded!");

        if (tooManySlots)
        {
            embed.AddField("`⚠️` Warning",
                $"This outfit has more sprite slots than you have unlocked.\n" +
                $"Some slots have been skipped to fit your current inventory.");
        }

        var colorMaps = outfit.SpriteSlotConfiguration
            .Where(spt => spt.ColorShift != null && spt.Slot > 0)
            .Select(slot => new ColorMapMessage { HueShift = slot.ColorShift ?? 100, SpriteId = slot.SpriteId });

        var combo = outfit.SpriteSlotConfiguration.Where(slot => slot.Slot > 0).OrderBy(slot => slot.Slot)
            .Select(slot => slot.SpriteId);
        var imageFile = await imageGeneratorClient.GenerateSpriteCombo(new GenerateComboMessage()
            { SpriteIds = { combo }, ColorMaps = { colorMaps } }).CollectFileChunksAsync();

        await context.RespondAsync(
            embed.ToMessageBuilderWithAttachmentImage(imageFile.FileName, imageFile.Data));
    }

    /// <summary>
    /// View the configuration of saved outfit
    /// </summary>
    /// <param name="context"></param>
    /// <param name="name">The name of the outfit that will be shown</param>
    [Command("view"), TextAlias("vw"), RequirePalantirMember]
    public async Task ViewOutfit(CommandContext context, string name)
    {
        logger.LogTrace("ViewOutfit(context, name={name})", name);

        var member = memberContext.Member;
        OutfitMessage outfit;
        try
        {
            outfit = await outfitsClient.GetOutfitAsync(
                new GetOutfitRequest { Login = member.Login, OutfitName = name });
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        {
            await context.RespondAsync(embed: new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, $"Outfit `{name}` not found",
                    "Use `/outfit list` to see all your saved outfits."));
            return;
        }

        SceneReply? scene = null;
        SceneThemeReply? theme = null;
        if (outfit.SceneId is { } sceneIdVal)
        {
            var themes = await scenesClient.GetThemesOfScene(new GetSceneRequest { Id = sceneIdVal })
                .ToDictionaryAsync(scene => scene.Shift);
            theme = outfit.SceneShift is { } sceneShiftVal ? themes[sceneShiftVal] : null;
            scene = await scenesClient.GetSceneByIdAsync(new GetSceneRequest { Id = sceneIdVal });
        }

        var sprites = await spritesClient.GetAllSprites(new Empty()).ToListAsync();
        var combo = string.Join("\n", outfit.SpriteSlotConfiguration.Select(slot =>
        {
            var sprite = sprites.First(spt => spt.Id == slot.SpriteId);
            var shift = slot.ColorShift is not null ? $"(color shift: {slot.ColorShift})" : "";
            return $"{sprite.Id.AsTypoId()}  {sprite.Name}  {shift}";
        }));

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor("Viewing an user outfit")
            .WithTitle(name)
            .AddField("Combo", $"{(combo.Length == 0 ? "Empty" : combo)}")
            .AddField("Scene",
                $"{(scene is null ? "None" : $"{scene.Id.AsTypoId()} {scene.Name} {(theme is not null ? $"(Theme: {theme.Name})" : "")}")}")
            .WithDescription($"You can use this outfit with `/outfit use {name}`. \n" +
                             $"To update it to your current style, use `/outfit save {name}`.\n" +
                             $"To delete the outfit, use `/outfit delete {name}`.");

        var colorMaps = outfit.SpriteSlotConfiguration
            .Where(spt => spt.ColorShift != null && spt.Slot > 0)
            .Select(slot => new ColorMapMessage { HueShift = slot.ColorShift ?? 100, SpriteId = slot.SpriteId });

        var comboIds = outfit.SpriteSlotConfiguration.Where(slot => slot.Slot > 0).OrderBy(slot => slot.Slot)
            .Select(slot => slot.SpriteId);
        var imageFile = await imageGeneratorClient.GenerateSpriteCombo(new GenerateComboMessage
            { SpriteIds = { comboIds }, ColorMaps = { colorMaps } }).CollectFileChunksAsync();

        await context.RespondAsync(
            embed.ToMessageBuilderWithAttachmentImage(imageFile.FileName, imageFile.Data));
    }
}