using DSharpPlus.Commands;
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
using Enum = System.Enum;

namespace tobeh.Palantir.Commands.Commands;

/// <summary>
/// View, buy and use sprites
/// </summary>
/// <param name="logger"></param>
/// <param name="memberContext"></param>
/// <param name="spritesClient"></param>
/// <param name="inventoryClient"></param>
/// <param name="imageGeneratorClient"></param>
/// <param name="eventsClient"></param>
[Command("sprite")]
[TextAlias("spt")]
public class SpriteCommands(
    ILogger<SpriteCommands> logger,
    MemberContext memberContext,
    Sprites.SpritesClient spritesClient,
    Inventory.InventoryClient inventoryClient,
    ImageGenerator.ImageGeneratorClient imageGeneratorClient,
    Events.EventsClient eventsClient)
{
    /// <summary>
    /// View all sprites in your inventory
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="Exception"></exception>
    [Command("inventory"), TextAlias("inv"), RequirePalantirMember]
    public async Task ViewSpriteInventory(CommandContext context)
    {
        logger.LogTrace("ViewSpriteInventory(context)");

        var member = memberContext.Member;
        var inventory = await inventoryClient.GetSpriteInventory(new GetSpriteInventoryRequest { Login = member.Login })
            .ToListAsync();

        // get all sprites, likely more performance than each individually
        var sprites = await spritesClient.GetAllSprites(new Empty()).ToDictionaryAsync(sprite => sprite.Id);
        var ranks = await spritesClient.GetSpriteRanking(new Empty()).ToListAsync();

        var userSprites = inventory
            .Select(sprite => sprites[sprite.SpriteId])
            .OrderBy(spt => spt.Id).ToList();
        var eventSpriteCount = userSprites.Count(sprite => sprite.EventDropId > 0);
        var totalWorth = userSprites
            .Where(sprite => sprite.EventDropId is null)
            .Sum(sprite => sprite.Cost);

        var uniquenessMaxUsers = ranks.Max(rank => rank.TotalBought);
        var uniquenessUserScore = ranks
            .Where(rank => inventory.Any(slot => slot.SpriteId == rank.Id))
            .Select(rank => rank.TotalBought * 100 / uniquenessMaxUsers)
            .Average();

        // batch sprites to 45 per page
        const int batchSize = 45;
        var pages = userSprites.Chunk(batchSize).Select((batch, idx) => new
        {
            Page = idx + 1,
            Sprites = batch
        }).Select(page =>
        {
            var spriteNumberStart = batchSize * (page.Page - 1) + 1;
            var embed = new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithAuthor(
                    $"Viewing sprites {spriteNumberStart} - {spriteNumberStart + page.Sprites.Count() - 1} of {inventory.Count}")
                .WithTitle("Sprite Inventory");

            embed.AddField("Total worth:", $"`🫧` {totalWorth} Bubbles");
            embed.AddField("Event sprites:", $"`🎟️` {eventSpriteCount} Sprites collected");
            embed.AddField("Uniqueness:",
                $"`💎` Your inventory has an uniqueness score of {100 - Math.Round(uniquenessUserScore)}%");

            if (inventory.Count < 5)
            {
                embed.AddField("Command help:", "Use `/sprite buy <id>` to buy a sprite\n" +
                                                "Use `/sprite use <id>` to wear a sprite\n" +
                                                "Use `/sprite color <id> [color]` to colorize a rainbow sprite");
            }

            foreach (var fieldBatch in page.Sprites.Chunk(5))
            {
                embed.AddField("_ _",
                    string.Join("\n",
                        fieldBatch.Select(sprite =>
                            $"`{sprite.Id.AsTypoId()}`{(sprite.IsRainbow ? " `🌈`" : "")}{(sprite.IsSpecial ? " `✨`" : "")} {sprite.Name}")),
                    true);
            }

            return embed;
        }).ToList();

        await context.RespondPalantirPaginationAsync(pages, "Sprites");
    }

    /// <summary>
    /// View the most popular sprites
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="Exception"></exception>
    [Command("list"), TextAlias("ls")]
    public async Task ListSprites(CommandContext context)
    {
        logger.LogTrace("ListSprites(context)");

        var sprites = await spritesClient.GetAllSprites(new Empty()).ToListAsync();
        var ranks = await spritesClient.GetSpriteRanking(new Empty()).ToListAsync();

        var ranked = sprites
            .Select(sprite => new { Sprite = sprite, Rank = ranks.FirstOrDefault(rank => rank.Id == sprite.Id) })
            .OrderBy(rank => rank.Rank?.Rank ?? ranks.Count)
            .ToList();

        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithDescription("Sprites are ranked by their total purchases and active users.\n" +
                             $"View all sprites {"here".AsTypoLink("https://typo.rip/tools/sprites", "🌍")}")
            .WithTitle("Sprite Ranking");

        foreach (var sprite in ranked.Take(10))
        {
            embedBuilder.AddField($"**#{sprite.Rank?.Rank}** {sprite.Sprite.Id.AsTypoId()} _ _ {sprite.Sprite.Name}",
                $"{sprite.Rank?.TotalBought} bought, {sprite.Rank?.ActiveUsers} active");
        }

        await context.RespondAsync(embedBuilder.Build());
    }

    /// <summary>
    /// View the details of a sprite
    /// </summary>
    /// <param name="context"></param>
    /// <param name="spriteId">The ID of the sprite to show</param>
    /// <exception cref="Exception"></exception>
    [DefaultGroupCommand, Command("view"), TextAlias("vw")]
    public async Task ViewSprite(CommandContext context, uint spriteId)
    {
        logger.LogTrace("ViewSprite(context, {spriteId})", spriteId);

        var sprite = await spritesClient.GetSpriteByIdAsync(new GetSpriteRequest { Id = (int)spriteId });

        if (!sprite.IsReleased)
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Sprite not released", "This sprite is not released yet. Stay tuned!"));
            return;
        }

        var ranking = await spritesClient.GetSpriteRanking(new Empty()).ToListAsync();
        var spriteRank = ranking.Find(s => s.Id == spriteId) ??
                         throw new Exception("Failed to calculate sprite ranking");

        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"{sprite.Id.AsTypoId()} _ _ {sprite.Name}")
            .WithImageUrl(sprite.Url);

        if (sprite.EventDropId is { } eventDropId)
        {
            var drop = await eventsClient.GetEventDropByIdAsync(new GetEventDropRequest { Id = eventDropId });
            var spriteEvent = await eventsClient.GetEventByIdAsync(new GetEventRequest { Id = drop.EventId });

            embedBuilder.WithThumbnail(drop.Url);
            embedBuilder.AddField("Event Sprite:", $"{spriteEvent.Name} Event {spriteEvent.Id.AsTypoId()}\n" +
                                                   $"Buyable with {drop.Name} Drops");
            embedBuilder.AddField("Price:", $"`💧` {sprite.Cost} {drop.Name} Drops");
        }
        else
        {
            embedBuilder.AddField("Price:", $"`🫧` {sprite.Cost} Bubbles");
        }

        if (sprite.IsRainbow) embedBuilder.AddField("Rainbow Sprite:", $"`🌈` This sprite is color-customizable");
        if (sprite.IsSpecial) embedBuilder.AddField("Background Sprite:", $"`✨` This sprite replaces the avatar");
        if (sprite.RequiredFlags.Count > 0)
        {
            var enumValues = Enum.GetValues<MemberFlagMessage>().ToList();
            var requiredFlags = sprite.RequiredFlags.Select(flag => enumValues.First(v => v == flag).ToString());
            embedBuilder.AddField("Requirements:", $"`🔒` {string.Join(" or ", requiredFlags)}");
        }

        embedBuilder.AddField("Artist:", $"`🖌️` Created by {sprite.Artist ?? "tobeh"}");
        embedBuilder.AddField("Ranking:",
            $"`📈` #{spriteRank.Rank}: {spriteRank.TotalBought} bought, {spriteRank.ActiveUsers} active \n" +
            $"View all sprites {"here".AsTypoLink("https://typo.rip/tools/sprites", "🌍")}");

        await context.RespondAsync(embedBuilder.Build());
    }

    /// <summary>
    /// Buy a new sprite
    /// </summary>
    /// <param name="context"></param>
    /// <param name="spriteId">The ID of the sprite that will be added to your inventory</param>
    [Command("buy"), RequirePalantirMember]
    public async Task BuySprite(CommandContext context, uint spriteId)
    {
        logger.LogTrace("BuySprite(context, {spriteId})", spriteId);

        var sprite = await spritesClient.GetSpriteByIdAsync(new GetSpriteRequest { Id = (int)spriteId });

        // unreleased sprites - eg progressive event sprites - may only be bought after a certain date
        if (!sprite.IsReleased)
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Sprite not released", "This sprite is not released yet. Stay tuned!"));
            return;
        }

        // check if the user has bought this sprite already
        var member = memberContext.Member;
        var inventory = await inventoryClient.GetSpriteInventory(new GetSpriteInventoryRequest { Login = member.Login })
            .ToListAsync();
        if (inventory.Any(slot => slot.SpriteId == sprite.Id))
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Sprite already bought",
                $"You already own {sprite.Name} {sprite.Id.AsTypoId()}. You can use it with `/sprite use {sprite.Id}`."));
            return;
        }

        // check if the user is eligible to buy the sprite
        if (sprite.RequiredFlags.All(flag => !member.MappedFlags.Contains(flag)))
        {
            var enumValues = Enum.GetValues<MemberFlagMessage>().ToList();
            var requiredFlags = sprite.RequiredFlags.Select(flag => enumValues.First(v => v == flag).ToString());
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Sprite is locked",
                $"You need one of the following flags to buy {sprite.Name} {sprite.Id.AsTypoId()}:\n {string.Join(", ", requiredFlags)}"));
            return;
        }

        // check if the credit of the user is sufficient
        if (sprite.EventDropId is { } eventDropId)
        {
            var drop = await eventsClient.GetEventDropByIdAsync(new GetEventDropRequest { Id = eventDropId });
            var eventInventory = await inventoryClient.GetEventCredit(new GetEventCreditRequest
                { Login = member.Login, EventId = drop.EventId }).ToListAsync();
            var credit = eventInventory.FirstOrDefault(credit => credit.EventDropId == drop.Id)?.AvailableCredit ?? 0;

            if (credit < sprite.Cost)
            {
                await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                    "Event credit too low",
                    $"You need {sprite.Cost} {drop.Name} Drop(s) to buy {sprite.Name} {sprite.Id.AsTypoId()}, but you only have {credit} available.\n" +
                    $"Collect more {drop.Name} Drops on skribbl or check in `/event view {drop.EventId}` if you have some league event drops to redeem!"));
                return;
            }
        }
        else
        {
            var bubbleCredit =
                await inventoryClient.GetBubbleCreditAsync(new GetBubbleCreditRequest { Login = member.Login });
            if (bubbleCredit.AvailableCredit < sprite.Cost)
            {
                await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                    "Bubble credit too low",
                    $"You need {sprite.Cost} Bubbles to buy {sprite.Name} {sprite.Id.AsTypoId()}, but you only have {bubbleCredit.AvailableCredit} available.\n" +
                    $"Collect more Bubbles by playing skribbl or catch drops to get bonus bubbles!\nYou can see your bubble credit with `/inventory`."));
                return;
            }
        }

        // buy sprite
        await inventoryClient.BuySpriteAsync(new BuySpriteRequest { Login = member.Login, SpriteId = sprite.Id });
        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor("You unlocked a new sprite!")
            .WithTitle($"{sprite.Id.AsTypoId()} _ _ {sprite.Name}")
            .WithImageUrl(sprite.Url);

        embedBuilder.AddField("Wear it:",
            $"`👕` Use the command `/sprite use {sprite.Id}` to wear this sprite on your skribbl avatar.");
        if (sprite.IsRainbow)
            embedBuilder.AddField("Customize color:",
                $"`🌈` Use the command `/sprite color {sprite.Id} (color)` to change the color of this sprite.");
        if (sprite.IsSpecial)
            embedBuilder.AddField("Background Sprite:", $"`✨` This sprite replaces the avatar, when you wear it.");

        await context.RespondAsync(embedBuilder.Build());
    }

    /// <summary>
    /// Choose a sprite to wear on your avatar on skribbl.
    /// To remove a sprite on a slot, use `0` as the sprite ID.
    /// The default slot is `1`.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="spriteId">The ID of a sprite</param>
    /// <param name="slot">The sprite slot where the sprite will be used on</param>
    [Command("use"), RequirePalantirMember]
    public async Task UseSprite(CommandContext context, uint spriteId, uint slot = 1)
    {
        logger.LogTrace("UseSprite(context, {spriteId}, {slot})", spriteId, slot);

        var sprite = spriteId == 0
            ? null
            : await spritesClient.GetSpriteByIdAsync(new GetSpriteRequest { Id = (int)spriteId });
        var member = memberContext.Member;
        var inventory = await inventoryClient.GetSpriteInventory(new GetSpriteInventoryRequest { Login = member.Login })
            .ToListAsync();

        // check if the user owns this sprite
        if (sprite is not null && inventory.All(invSlot => invSlot.SpriteId != spriteId))
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Sprite not in inventory",
                    $"You don't own the sprite {sprite.Name} {sprite.Id.AsTypoId()} yet.\n" +
                    $"You can buy it with `/sprite buy {spriteId}`."));
            return;
        }

        // check if the user has enough sprite slots unlocked
        var slotCount =
            await inventoryClient.GetSpriteSlotCountAsync(new GetSpriteSlotCountRequest { Login = member.Login });
        if (slot > slotCount.UnlockedSlots)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Slot not unlocked",
                    $"You need to unlock more sprite slots to use {sprite?.Name ?? "sprites"} {sprite?.Id.AsTypoId() ?? ""} on slot {slot} ({slotCount.UnlockedSlots} slot(s) available).\n" +
                    $"For each 1000 drops, you unlock an additional slot.\n" +
                    $"{"Patrons".AsTypoLink("https://www.patreon.com/skribbltypo", "🩵")} get one slot on top."));
            return;
        }

        // activate new sprite
        await inventoryClient.UseSpriteComboAsync(new UseSpriteComboRequest
        {
            ClearOtherSlots = false,
            Combo = { new SpriteSlotConfigurationRequest { SpriteId = sprite?.Id, SlotId = (int)slot } },
            Login = member.Login
        });

        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor(sprite is null ? "You cleared a sprite slot." : "You activated a sprite!")
            .WithTitle(sprite is null ? "Such empty 💨" : $"{sprite.Id.AsTypoId()} _ _ {sprite.Name}")
            .WithImageUrl(sprite?.Url ?? "");

        if (sprite is not null)
            embedBuilder.WithDescription(
                $"The sprite {sprite.Name} will now be displayed on your skribbl avatar on slot {slot}.\n" +
                $"To remove it, use the command `/sprite use 0 {slot}`.");
        else embedBuilder.WithDescription($"The sprite on slot {slot} has been removed.");

        await context.RespondAsync(embedBuilder.Build());
    }

    /// <summary>
    /// Choose a sprite combination to wear on your avatar on skribbl.
    /// This overwrites all other currently set sprites.
    /// To quickly remove all sprites, use '0' as combo.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="combo">A sequence of sprite IDs, in order of slots. To leave a slot empty, use 0</param>
    [Command("combo"), TextAlias("cb"), RequirePalantirMember]
    public async Task UseCombo(CommandContext context, params int[] combo)
    {
        logger.LogTrace("UseCombo(context, {combo})", combo);

        var member = memberContext.Member;
        var inventory = await inventoryClient.GetSpriteInventory(new GetSpriteInventoryRequest { Login = member.Login })
            .ToListAsync();

        // check if the user owns all sprites
        if (combo.Any(id => id > 0 && inventory.All(invSlot => invSlot.SpriteId != id)))
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Sprite not in inventory",
                    $"You don't own all sprites from the selected combo yet."));
            return;
        }

        // check if the user has enough sprite slots unlocked
        var slotCount =
            await inventoryClient.GetSpriteSlotCountAsync(new GetSpriteSlotCountRequest { Login = member.Login });
        if (combo.Length > slotCount.UnlockedSlots)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Slot not unlocked",
                    $"You need to unlock more sprite slots to use this combo with {combo.Length} sprites ({slotCount.UnlockedSlots} slot(s) available).\n" +
                    $"For each 1000 drops, you unlock an additional slot.\n" +
                    $"{"Patrons".AsTypoLink("https://www.patreon.com/skribbltypo", "🩵")} get one slot on top."));
            return;
        }

        // activate new sprite
        await inventoryClient.UseSpriteComboAsync(new UseSpriteComboRequest
        {
            ClearOtherSlots = true,
            Combo =
            {
                combo.Select((id, idx) => new SpriteSlotConfigurationRequest
                    { SpriteId = id == 0 ? null : id, SlotId = idx + 1 })
            },
            Login = member.Login
        });

        var comboIsEmpty = combo.All(c => c == 0);
        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor(comboIsEmpty ? "You cleared your sprite combo." : "You activated a sprite combo!")
            .WithTitle(comboIsEmpty ? "Such empty 💨" : $"{combo.Length} slots filled")
            .WithDescription($"This sprite combo will now be displayed on your skribbl avatar.\n" +
                             $"To clear the combo, use the command `/sprite combo 0`.");

        var colorMaps = inventory
            .Where(spt => spt.ColorShift != null)
            .Select(slot => new ColorMapMessage { HueShift = slot.ColorShift ?? 100, SpriteId = slot.SpriteId });

        var imageFile = await imageGeneratorClient.GenerateSpriteCombo(new GenerateComboMessage()
            { SpriteIds = { combo }, ColorMaps = { colorMaps } }).CollectFileChunksAsync();

        await context.RespondAsync(
            embedBuilder.ToMessageBuilderWithAttachmentImage(imageFile.FileName, imageFile.Data));
    }

    /// <summary>
    /// Choose a color modification for one of your rainbow sprites.
    /// Leave "shift" empty to reset the color.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="spriteId">The ID of the sprite which will be colorized.</param>
    /// <param name="shift">A number from 0-200 to modify your sprite color. 100 is the original color.</param>
    [Command("color"), TextAlias("col"), RequirePalantirMember]
    public async Task UseSpriteColorConfig(CommandContext context, uint spriteId, uint? shift = null)
    {
        logger.LogTrace("UseSpriteColorConfig(context, {shift})", shift);

        var member = memberContext.Member;
        var inventory = await inventoryClient.GetSpriteInventory(new GetSpriteInventoryRequest { Login = member.Login })
            .ToListAsync();

        // check if the user owns the sprite
        if (inventory.All(slot => slot.SpriteId != spriteId))
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Sprite not in inventory", $"You don't own the selected sprite."));
            return;
        }

        // check if the sprite can be color customized
        var sprite = await spritesClient.GetSpriteByIdAsync(new GetSpriteRequest { Id = (int)spriteId });
        if (!sprite.IsRainbow)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Sprite is no Rainbow Sprite",
                    $"The sprite {sprite.Name} {sprite.Id.AsTypoId()} is not color-customizable."));
            return;
        }

        // check if the user can colorize another sprite (patron)
        var moreThanOneUnlocked =
            member.MappedFlags.Any(flag => flag is MemberFlagMessage.Admin or MemberFlagMessage.Patron);
        var otherConfig = inventory.FirstOrDefault(slot => slot.SpriteId != spriteId && slot.ColorShift is not null);
        bool clearOther = shift is not null && !moreThanOneUnlocked && otherConfig is not null;

        // apply rainbow config
        await inventoryClient.SetSpriteColorConfigurationAsync(new SetSpriteColorRequest
        {
            Login = member.Login, ClearOtherConfigs = clearOther,
            ColorConfig = { new SpriteColorConfigurationRequest { SpriteId = sprite.Id, ColorShift = (int?)shift } }
        });


        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor(shift is null ? "You cleared your sprite rainbow color." : "You colorized a rainbow sprite!")
            .WithTitle($"{sprite.Id.AsTypoId()} _ _ {sprite.Name}")
            .WithImageUrl(shift is null
                ? sprite.Url
                : $"https://static.typo.rip/sprites/rainbow/modulate.php?url={sprite.Url}&hue={shift}");

        if (clearOther)
        {
            embedBuilder.AddField("Color limit", "Your other color configurations have been reset.\n" +
                                                 $"Become a {"Patron".AsTypoLink("https://www.patreon.com/skribbltypo", "🩵")} to colorize unlimited sprites!");
        }

        if (shift is not null)
        {
            embedBuilder.WithDescription($"This sprite will now have its unique color!\n" +
                                         $"To clear the color, use the command `/sprite color {sprite.Id}`.");
        }
        else
        {
            embedBuilder.WithDescription($"This sprite will now have its original color!\n" +
                                         $"To colorize it, use the command `/sprite color {sprite.Id} (color)`.");
        }

        await context.RespondAsync(embedBuilder.Build());
    }
}