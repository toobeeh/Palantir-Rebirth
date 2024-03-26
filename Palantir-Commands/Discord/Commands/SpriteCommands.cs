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

[Command("sprite")]
[TextAlias("spt")]
[RequirePalantirMember(MemberFlagMessage.Beta)]
public class SpriteCommands(
    ILogger<SpriteCommands> logger, 
    Sprites.SpritesClient spritesClient, 
    Inventory.InventoryClient inventoryClient,
    Members.MembersClient membersClient,
    Events.EventsClient eventsClient)
{
    
    /// <summary>
    /// View all sprites in your inventory
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="Exception"></exception>
    [Command("inventory")]
    [TextAlias("inv")]
    public async Task ViewSpriteInventory(CommandContext context)
    {
        logger.LogTrace("ViewSpriteInventory(context)");
        
        var member = await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest {Id = (long)context.User.Id});
        var inventory = await inventoryClient.GetSpriteInventory(new GetSpriteInventoryRequest { Login = member.Login }).ToListAsync();
        
        // get all sprites, likely more performance than each individually
        var sprites = await spritesClient.GetAllSprites(new Empty()).ToListAsync();
        var ranks = await spritesClient.GetSpriteRanking(new Empty()).ToListAsync();

        var userSprites = sprites.Where(sprite => inventory.Any(slot => slot.SpriteId == sprite.Id)).ToList();
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
        var pages = inventory.Batch(batchSize).Select((batch, idx) => new
        {
            Page = idx + 1,
            Sprites = batch.Select(slot => slot.SpriteId)
        }).Select(page =>
        {
            var spriteNumberStart = batchSize * (page.Page - 1) + 1;
            var embed =  new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithAuthor($"Viewing sprites {spriteNumberStart} - {spriteNumberStart + page.Sprites.Count() - 1} of {inventory.Count}")
                .WithTitle("Sprite Inventory");
            
            embed.AddField("Total worth:", $"`🫧` {totalWorth} Bubbles");
            embed.AddField("Event sprites:", $"`🎟️` {eventSpriteCount} Sprites collected");
            embed.AddField("Uniqueness:", $"`💎` Your inventory has an uniqueness score of {100 - Math.Round(uniquenessUserScore)}%");

            if (inventory.Count < 5)
            {
                embed.AddField("Command help:", "Use `/sprite buy (id)` to buy a sprite\n" +
                                                "Use `/sprite use (id)` to wear a sprite\n" +
                                                "Use `/sprite color (id) (color)` to colorize a rainbow sprite");
            }
            
            foreach (var fieldBatch in page.Sprites.Batch(5))
            {
                var fieldSprites = sprites.Where(sprite => fieldBatch.Contains(sprite.Id));
                embed.AddField("_ _", string.Join("\n", fieldSprites.Select(sprite => $"`{sprite.Id.AsTypoId()}`{(sprite.IsRainbow ? " `🌈`" : "")}{(sprite.IsSpecial ? " `✨`" : "")} {sprite.Name}")), true);
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
    [Command("list")]
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
            embedBuilder.AddField($"**#{sprite.Rank?.Rank}** {sprite.Sprite.Id.AsTypoId()} _ _ {sprite.Sprite.Name}", $"{sprite.Rank?.TotalBought} bought, {sprite.Rank?.ActiveUsers} active");
        }

        await context.RespondAsync(embedBuilder.Build());
    }
    
    /// <summary>
    /// View the details of a sprite
    /// </summary>
    /// <param name="context"></param>
    /// <param name="spriteId">The ID of the sprite to show</param>
    /// <exception cref="Exception"></exception>
    [DefaultGroupCommand]
    [Command("view")]
    public async Task ViewSprite(CommandContext context, int spriteId)
    {
        logger.LogTrace("ViewSprite(context, {spriteId})", spriteId);
        
        var sprite = await spritesClient.GetSpriteByIdAsync(new GetSpriteRequest { Id = spriteId });

        if (!sprite.IsReleased)
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context, "Sprite not released", "This sprite is not released yet. Stay tuned!"));
            return;
        }
        
        var ranking = await spritesClient.GetSpriteRanking(new Empty()).ToListAsync();
        var spriteRank = ranking.Find(s => s.Id == spriteId) ?? throw new Exception("Failed to calculate sprite ranking");
        
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
        
        if(sprite.IsRainbow) embedBuilder.AddField("Rainbow Sprite:", $"`🌈` This sprite is color-customizable");
        if(sprite.IsSpecial) embedBuilder.AddField("Background Sprite:", $"`✨` This sprite replaces the avatar");
        
        embedBuilder.AddField("Artist:", $"`🖌️` Created by {sprite.Artist ?? "tobeh"}");
        embedBuilder.AddField("Ranking:", $"`📈` #{spriteRank.Rank}: {spriteRank.TotalBought} bought, {spriteRank.ActiveUsers} active \n" +
                                          $"View all sprites {"here".AsTypoLink("https://typo.rip/tools/sprites", "🌍")}");
        
        await context.RespondAsync(embedBuilder.Build());
    }
    
    [Command("buy")]
    public async Task BuySprite(CommandContext context, int spriteId)
    {
        logger.LogTrace("BuySprite(context, {spriteId})", spriteId);
        
        var sprite = await spritesClient.GetSpriteByIdAsync(new GetSpriteRequest { Id = spriteId });

        // unreleased sprites - eg progressive event sprites - may only be bought after a certain date
        if (!sprite.IsReleased)
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context, "Sprite not released", "This sprite is not released yet. Stay tuned!"));
            return;
        }
        
        // check if the user has bought this sprite already
        var user = await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest {Id = (long)context.User.Id});
        var inventory = await inventoryClient.GetSpriteInventory(new GetSpriteInventoryRequest { Login = user.Login }).ToListAsync();
        if(inventory.Any(slot => slot.SpriteId == sprite.Id))
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context, "Sprite already bought", $"You already own {sprite.Name} {sprite.Id.AsTypoId()}. You can use it with `/sprite use {sprite.Id}`."));
            return;
        }

        // check if the credit of the user is sufficient
        if (sprite.EventDropId is { } eventDropId)
        {
            var drop = await eventsClient.GetEventDropByIdAsync(new GetEventDropRequest { Id = eventDropId });
            var eventInventory = await inventoryClient.GetEventCredit(new GetEventCreditRequest { Login = user.Login, EventId = drop.EventId}).ToListAsync();
            var credit = eventInventory.FirstOrDefault(credit => credit.EventDropId == drop.Id)?.AvailableCredit ?? 0;
            
            if(credit < sprite.Cost) 
            {
                await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context, "Event credit too low", $"You need {sprite.Cost} {drop.Name} Drop(s) to buy {sprite.Name} {sprite.Id.AsTypoId()}, but you only have {credit} available.\n" +
                    $"Collect more {drop.Name} Drops on skribbl or check in `/event` if you have some league event drops to redeem!"));
                return;
            }
        }   
        else
        {   
            var bubbleCredit = await inventoryClient.GetBubbleCreditAsync(new GetBubbleCreditRequest { Login = user.Login });
            if(bubbleCredit.AvailableCredit < sprite.Cost) 
            {
                await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context, "Bubble credit too low", $"You need {sprite.Cost} Bubbles to buy {sprite.Name} {sprite.Id.AsTypoId()}, but you only have {bubbleCredit.AvailableCredit} available.\n" +
                    $"Collect more Bubbles by playing skribbl or catch drops to get bonus bubbles!"));
                return;
            }
        }
        
        // buy sprite
        await inventoryClient.BuySpriteAsync(new BuySpriteRequest { Login = user.Login, SpriteId = sprite.Id });
        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor("You unlocked a new sprite!")
            .WithTitle($"{sprite.Id.AsTypoId()} _ _ {sprite.Name}")
            .WithImageUrl(sprite.Url);
        
        embedBuilder.AddField("Wear it:", $"`👕` Use the command `/sprite use {sprite.Id}` to wear this sprite on your skribbl avatar.");
        if(sprite.IsRainbow) embedBuilder.AddField("Customize color:", $"`🌈` Use the command `/sprite color {sprite.Id} (color)` to change the color of this sprite.");
        if(sprite.IsSpecial) embedBuilder.AddField("Background Sprite:", $"`✨` This sprite replaces the avatar, when you wear it.");
        
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
    [Command("use")]
    public async Task UseSprite(CommandContext context, int spriteId, uint slot = 1)
    {
        logger.LogTrace("UseSprite(context, {spriteId}, {slot})", spriteId, slot);
        
        var sprite = spriteId == 0 ? null : await spritesClient.GetSpriteByIdAsync(new GetSpriteRequest { Id = spriteId });
        var user = await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest {Id = (long)context.User.Id});
        var inventory = await inventoryClient.GetSpriteInventory(new GetSpriteInventoryRequest { Login = user.Login }).ToListAsync();
        
        // check if the user owns this sprite
        if(sprite is not null && inventory.All(invSlot => invSlot.SpriteId != spriteId))
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Sprite not in inventory", $"You don't own the sprite {sprite.Name} {sprite.Id.AsTypoId()} yet.\n" +
                                                                              $"You can buy it with `/sprite buy {spriteId}`."));
            return;
        }
        
        // check if the user has enough sprite slots unlocked
        var slotCount = await inventoryClient.GetSpriteSlotCountAsync(new GetSpriteSlotCountRequest { Login = user.Login });
        if(slot > slotCount.UnlockedSlots)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Slot not unlocked", $"You need to unlock more sprite slots to use {sprite?.Name ?? "sprites"} {sprite?.Id.AsTypoId() ?? ""} on slot {slot} ({slotCount.UnlockedSlots} slot(s) available).\n" +
                                                                        $"For each 1000 drops, you unlock an additional slot.\n" +
                                                                        $"{"Patrons".AsTypoLink("https://www.patreon.com/skribbltypo", "🩵")} get one slot on top."));
            return;
        }
        
        // activate new sprite
        await inventoryClient.UseSpriteComboAsync(new UseSpriteComboRequest
        {
            ClearOtherSlots = false,
            Combo = { new SpriteSlotConfigurationRequest { SpriteId = spriteId, SlotId = (int)slot } },
            Login = user.Login
        });
        
        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor(sprite is null ? "You cleared a sprite slot." : "You activated a sprite!")
            .WithTitle(sprite is null ? "Such empty 💨" : $"{sprite.Id.AsTypoId()} _ _ {sprite.Name}")
            .WithImageUrl(sprite?.Url ?? "");
        
        embedBuilder.WithDescription($"This sprite will now be displayed on your skribbl avatar on slot {slot}.\n" +
                                     $"To remove it, use the command `/sprite use 0 {slot}`.");
        
        await context.RespondAsync(embedBuilder.Build());
    }
    
    /// <summary>
    /// Choose a sprite combination to wear on your avatar on skribbl.
    /// This overwrites all other currently set sprites.
    /// To quickly remove all sprites, use '0' as combo.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="combo">A sequence of sprite IDs, in order of slots</param>
    [Command("combo")]
    public async Task UseCombo(CommandContext context, params int[]? combo)
    {
        logger.LogTrace("UseCombo(context, {combo})", combo);
        
        var user = await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest {Id = (long)context.User.Id});
        var inventory = await inventoryClient.GetSpriteInventory(new GetSpriteInventoryRequest { Login = user.Login }).ToListAsync();
        
        // check if the user owns all sprites
        if(combo is not null && combo.Any(id => id > 0 && inventory.All(invSlot => invSlot.SpriteId != id)))
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Sprite not in inventory", $"You don't own all sprites from the selected combo yet."));
            return;
        }
        
        // check if the user has enough sprite slots unlocked
        var slotCount = await inventoryClient.GetSpriteSlotCountAsync(new GetSpriteSlotCountRequest { Login = user.Login });
        if(combo is not null && combo.Length > slotCount.UnlockedSlots)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Slot not unlocked", $"You need to unlock more sprite slots to use this combo with {combo.Length} sprites ({slotCount.UnlockedSlots} slot(s) available).\n" +
                                                                        $"For each 1000 drops, you unlock an additional slot.\n" +
                                                                        $"{"Patrons".AsTypoLink("https://www.patreon.com/skribbltypo", "🩵")} get one slot on top."));
            return;
        }
        
        // activate new sprite
        await inventoryClient.UseSpriteComboAsync(new UseSpriteComboRequest
        {
            ClearOtherSlots = true,
            Combo = { combo is null ? [] : combo.Select((id, idx) => new SpriteSlotConfigurationRequest { SpriteId = id, SlotId = idx + 1}) },
            Login = user.Login
        });
        
        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor(combo?.Length == 0 ? "You cleared your sprite combo." : "You activated a sprite combo!")
            .WithTitle(combo?.Length == 0 ? "Such empty 💨" : $"{combo?.Length ?? 0} Sprites selected")
            .WithImageUrl(""); // TODO generate combo image
        
        embedBuilder.WithDescription($"This sprite combo will now be displayed on your skribbl avatar.\n" +
                                     $"To clear the combo, use the command `/sprite combo 0`.");
        
        await context.RespondAsync(embedBuilder.Build());
    }
    
    /// <summary>
    /// Choose a color modification for one of your rainbow sprites.
    /// Leave "shift" empty to reset the color.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="spriteId">The ID of the sprite which will be colorized.</param>
    /// <param name="shift">A number from 0-200 to modify your sprite color. 100 is the original color.</param>
    [Command("color")]
    public async Task UseSpriteColorConfig(CommandContext context, int spriteId, int? shift = null)
    {
        logger.LogTrace("UseSpriteColorConfig(context, {shift})", shift);

        
        var user = await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest {Id = (long)context.User.Id});
        var inventory = await inventoryClient.GetSpriteInventory(new GetSpriteInventoryRequest { Login = user.Login }).ToListAsync();
        
        // check if the user owns the sprite
        if(!inventory.Any(slot => slot.SpriteId == spriteId))
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Sprite not in inventory", $"You don't own the selected sprite."));
            return;
        }
        
        // check if the sprite can be color customized
        var sprite = await spritesClient.GetSpriteByIdAsync(new GetSpriteRequest { Id = spriteId });
        if(!sprite.IsRainbow)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Sprite is no Rainbow Sprite", $"The sprite {sprite.Name} {sprite.Id.AsTypoId()} is not color-customizable."));
            return;
        }
        
        // check if the user can colorize another sprite (patron)
        var moreThanOneUnlocked = user.MappedFlags.Any(flag => flag is MemberFlagMessage.Admin or MemberFlagMessage.Patron);
        var otherConfig = inventory.FirstOrDefault(slot => slot.SpriteId != spriteId && slot.ColorShift is not null);
        if (shift is not null && !moreThanOneUnlocked && otherConfig is not null)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Rainbow Sprite Limit", $"You need to be a {"Patron".AsTypoLink("https://www.patreon.com/skribbltypo", "🩵")} to colorize more than one sprite at once.\n" +
                                                                    $"Use `/sprite color {otherConfig.SpriteId}` to reset the color of your current rainbow sprite."));
            return;
        }
        
        // apply rainbow config
        await inventoryClient.SetSpriteColorConfigurationAsync(new SetSpriteColorRequest
        {
            Login = user.Login, ClearOtherConfigs = false,
            ColorConfig = { new SpriteColorConfigurationRequest { SpriteId = sprite.Id, ColorShift = shift } }
        });
        
        
        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor(shift is null ? "You cleared your sprite rainbow color." : "You colorized a rainbow sprite!")
            .WithTitle($"{sprite.Id.AsTypoId()} _ _ {sprite.Name}")
            .WithImageUrl(shift is null ? sprite.Url : $"https://static.typo.rip/sprites/rainbow/modulate.php?url={sprite.Url}&hue={shift}");
        
        if(shift is not null)
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