using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands.Attributes;
using DSharpPlus.Commands.Trees.Attributes;
using DSharpPlus.Entities;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Palantir_Commands.Discord.Checks;
using Palantir_Commands.Discord.Extensions;
using Valmar;
using Valmar_Client.Grpc;

namespace Palantir_Commands.Discord.Commands;

[Command("event"), TextAlias("ev")]
public class EventCommands(
    ILogger<EventCommands> logger,
    Events.EventsClient eventsClient,
    Inventory.InventoryClient inventoryClient
    )
{
    
    [Command("list"), TextAlias("ls")]
    public async Task ViewEventList(CommandContext context)
    {
        logger.LogTrace("ViewEventList()");
        
        var events = await eventsClient.GetAllEvents(new Empty()).ToListAsync();

        var list = events
            .OrderByDescending(evt => evt.StartDate)
            .Select(evt => new
            {
                Title = $"{evt.Id.AsTypoId()} _ _ {evt.Name}",
                Description = $"```md\n- From {evt.StartDate.ToDateTimeOffset():d}\n- {evt.Length} days```"
            });
        
        const int pageSize = 8;
        var pages = list.Batch(pageSize).Select(page =>
        {
            var embed = new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithTitle("All Sprite Events")
                .WithDescription("This listing contains all events - planned, active and passed.\n" +
                                 "To view a single event and your progress, use the command `/event view (id)`.\n _ _ \n_ _")
                .WithDualColumnFields(page, item => item.Title, item => item.Description);
            
            return embed;
        }).ToList();

        await context.RespondPalantirPaginationAsync(pages);
    }
    
    [Command("view"), TextAlias("vw"), DefaultGroupCommand, RequirePalantirMember]
    public async Task ViewEvent(CommandContext context, uint? eventId = null)
    {
        throw new NotImplementedException();
    }
    
    [Command("gift"), TextAlias("gf"), RequirePalantirMember(MemberFlagMessage.Beta)]
    public async Task GiftEventDrops(CommandContext context, DiscordMember receiver, uint amount, int eventSprite)
    {
        throw new NotImplementedException();
    }
    
    [Command("redeem"), TextAlias("rd"), RequirePalantirMember(MemberFlagMessage.Beta)]
    public async Task RedeemLeagueEventDrops(CommandContext context, uint amount, int eventSprite)
    {
        throw new NotImplementedException();
    }
}