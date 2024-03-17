using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees.Attributes;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Palantir_Commands.Discord.Extensions;
using Valmar;

namespace Palantir_Commands.Discord.Commands;

[Command("sprite")]
public class SpriteCommands(ILogger<SpriteCommands> logger, Sprites.SpritesClient spritesClient, Events.EventsClient eventsClient)
{
    
    [Command("view")]
    public async Task ViewSprite(CommandContext context, int spriteId)
    {
        logger.LogTrace("ViewSprite(context, {spriteId})", spriteId);
        
        var sprite = await spritesClient.GetSpriteByIdAsync(new GetSpriteRequest { Id = spriteId });
        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"` #{sprite.Id} ` _ _ {sprite.Name}")
            .WithImageUrl(sprite.Url);

        if (sprite.EventDropId is { } eventDropId)
        {
            var drop = await eventsClient.GetEventDropByIdAsync(new GetEventDropRequest { Id = eventDropId });
            var spriteEvent = await eventsClient.GetEventByIdAsync(new GetEventRequest { Id = drop.EventId });

            embedBuilder.WithThumbnail(drop.Url);
            embedBuilder.AddField("Event Sprite:", $"On {spriteEvent.Name} Event ` #{spriteEvent.Id} `\n" +
                                                   $"Buyable with {drop.Name} Drops ` #{drop.Id} `", inline: false);
            embedBuilder.AddField("Price:", $"{sprite.Cost} {drop.Name} Drops", inline: false);
        }
        else
        {
            embedBuilder.AddField("Price:", $"{sprite.Cost} Bubbles", inline: false);
        }
        
        embedBuilder.AddField("Artist:", $"Created by {sprite.Artist ?? "tobeh"}", inline: false);
        embedBuilder.AddField("_ _", $"View all sprites [on the website](https://typo.rip/tools/sprites).");
        
        await context.RespondAsync(embedBuilder.Build());
    }
}