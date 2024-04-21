using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands.Attributes;
using DSharpPlus.Commands.Trees.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
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

[Command("event"), TextAlias("ev")]
public class EventCommands(
    ILogger<EventCommands> logger,
    Events.EventsClient eventsClient,
    Inventory.InventoryClient inventoryClient,
    Sprites.SpritesClient spritesClient,
    Scenes.ScenesClient scenesClient,
    Stats.StatsClient statsClient,
    Members.MembersClient membersClient,
    MemberContext memberContext
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
        logger.LogTrace("ViewEvent(eventId={eventId})", eventId);

        var member = memberContext.Member;
        var evt = eventId is {} eventIdValue ? 
            await eventsClient.GetEventByIdAsync(new GetEventRequest { Id = (int)eventIdValue }) : 
            await eventsClient.GetCurrentEventAsync(new Empty());
        
        var credit = await inventoryClient
            .GetEventCredit(new GetEventCreditRequest { Login = member.Login, EventId = evt.Id})
            .ToDictionaryAsync(credit => credit.EventDropId);
        var totalRedeemable = credit.Values.Sum(c => c.RedeemableAmount);
        
        var spriteInventory = await inventoryClient
            .GetSpriteInventory(new GetSpriteInventoryRequest { Login = member.Login })
            .ToListAsync();
        
        var sprites = await spritesClient
            .GetAllSprites(new Empty())
            .ToListAsync();
        
        var eventDrops = await eventsClient
            .GetEventDropsOfEvent(new GetEventRequest { Id = evt.Id })
            .ToListAsync();

        var sceneInventory = await inventoryClient
            .GetSceneInventoryAsync(new GetSceneInventoryRequest { Login = member.Login });

        var giftLossRate = await inventoryClient.GetGiftLossRateAsync(new GetGiftLossRateMessage
            { EventId = evt.Id, Login = member.Login });

        var dropGroups = eventDrops
            .OrderBy(drop => drop.Id)
            .Select(drop => new
            {
                Sprites = sprites.Where(spt => spt.EventDropId == drop.Id).ToList(),
                Drop = drop,
                Credit = credit[drop.Id],
            })
            .Select(group => new
            {
                Title = $"_ _",
                Description = group.Drop.ReleaseStart.ToDateTimeOffset() < DateTimeOffset.UtcNow ? 
                    $"```js\n💧 {group.Drop.Name} Drop```\n  " +
                    $"> **Drop Credit:**\n>  {group.Credit.TotalCredit} collected, {group.Credit.AvailableCredit} available\n>   {(evt.Progressive ? group.Credit.RedeemableAmount : totalRedeemable) :0.#} redeemable\n> **Sprites**:\n" +
                    $"{string.Join("\n", group.Sprites.Select(sprite =>
                        $"> {(spriteInventory.Any(slot => slot.SpriteId == sprite.Id) ? "`📦`" : "")} {sprite.Id.AsTypoId()} {sprite.Name}: {sprite.Cost} Drops"))}" : 
                    $"This drop will release on {Formatter.Timestamp(group.Drop.ReleaseStart.ToDateTimeOffset(), TimestampFormat.LongDate)}.\nStay tuned!"
            }).ToList();
        
        
        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor("Viewing your event progress")
            .WithTitle($"{evt.Id.AsTypoId()} {evt.Name} Event")
            .WithDescription(
                $"From {Formatter.Timestamp(evt.StartDate.ToDateTimeOffset(), TimestampFormat.LongDate)} to {Formatter.Timestamp(evt.EndDate.ToDateTimeOffset(), TimestampFormat.LongDate)} ({evt.Length} days)\n" +
                (evt.Progressive ? "This event is progressive. Event drops and sprites will reveal gradually!" : "") +
                (giftLossRate.CollectedDrops > 0 ? $"Your current gift loss rate is `{giftLossRate.LossRateBase*100 :0.#}%` ({giftLossRate.CollectedDrops :0.#}/{giftLossRate.RequiredDrops} drops)\nTo gift event drops of this event, use `/event gift (amount) 22`\n" : "") +
                $"\n```\n{evt.Description}```");

        try
        {
            var scene = await scenesClient.GetEventSceneAsync(new GetEventSceneRequest { EventId = evt.Id});
            var traceStartDate = Timestamp.FromDateTimeOffset(evt.StartDate.ToDateTimeOffset().AddDays(-1));
            var bubbleRange = await statsClient.GetBubbleTimespanRangeAsync(new BubbleTimespanRangeRequest { Login = member.Login, StartDate = traceStartDate, EndDate = evt.EndDate });
            var bubblesCollected = bubbleRange.EndAmount - bubbleRange.StartAmount;
            dropGroups.Add(new
            {
                Title = "_ _", Description = $"```prolog\n🖼️ Event Scene```\n" +
                                                         $"> **Scene:**\n> {(sceneInventory.SceneIds.Contains(scene.Scene.Id) ? "`📦`" : "")} {scene.Scene.Id.AsTypoId()} {scene.Scene.Name}\n" +
                                                         $"> This scene can be unlocked by collecting bubbles during the event.\n" +
                                                         $"> **Progress:**\n>  {bubblesCollected} / {scene.Price} bubbles\n"
            });
            embed.WithImageUrl(scene.Scene.Url);
        }
        catch
        {
            // expected if event has no scene
        }

        embed.WithDualColumnFields(dropGroups, drop => drop.Title, drop => drop.Description);
            

        await context.RespondAsync(embed);
    }
    
    [Command("gift"), TextAlias("gf"), RequirePalantirMember(MemberFlagMessage.Beta)]
    public async Task GiftEventDrops(CommandContext context, DiscordMember receiver, uint amount, int eventId)
    {
        logger.LogTrace("GiftEventDrops(receiver={receiver}, amount={amount}, eventId={eventId})", receiver, amount, eventId);
        
        var member = memberContext.Member;
        MemberReply receiverMember;
        try
        {
            receiverMember = await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest
                { Id = (long)receiver.Id });
        }
        catch (RpcException)
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context)
                .WithAuthor("Who dat")
                .WithTitle("Invalid gift recipient")
                .WithDescription("Make sure the recipient has a palantir account!"));
            return;
        }

        if (member.Login == receiverMember.Login)
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context)
                .WithAuthor("What is that even supposed to mean :(")
                .WithTitle("Invalid gift recipient")
                .WithDescription("You cannot gift yourself drops. Mention someone else to gift drops to."));
            return;
        }
        
        var evt = await eventsClient.GetEventByIdAsync(new GetEventRequest { Id = eventId });
        var credits = await inventoryClient.GetEventCredit(new GetEventCreditRequest { Login = member.Login, EventId = eventId}).ToDictionaryAsync(credit => credit.EventDropId);
        var drops = await eventsClient.GetEventDropsOfEvent(new GetEventRequest { Id = eventId}).ToDictionaryAsync(drop => drop.Id);
        var lossRate = await inventoryClient.GetGiftLossRateAsync(new GetGiftLossRateMessage { EventId = eventId, Login = member.Login });

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor($"Gift {amount} event drops")
            .WithTitle($"{evt.Id.AsTypoId()} {evt.Name} Event")
            .WithDescription(
                $"Using gifts, you can transfer some of your event drops to a friend.\n" +
                $"When you do that, Palantir will keep a small amount of the gift to prevent shady business.\n" +
                $"Your current gift loss rate is `{lossRate.LossRateBase * 100 :0.#}%` ({lossRate.CollectedDrops :0.#}/{lossRate.RequiredDrops} drops)\n_ _\n" +
                $"To send a gift, choose an event drop which you want to gift to {Formatter.Mention(receiver, true)}.");

        var message = new DiscordMessageBuilder()
            .AddEmbed(embed);

        var rebuildComponents = (bool selectDisabled, bool buttonDisabled, int selectedDropId) =>
        {
            var dropOptions = drops.Values
                .OrderByDescending(drop => drop.Id)
                .Select(drop => new DiscordSelectComponentOption(drop.Name, drop.Id.ToString(),
                    $"{credits[drop.Id].AvailableCredit} drops available", selectedDropId == drop.Id))
                .ToList();
            var select = new DiscordSelectComponent("dropId", "Choose an event drop", dropOptions, selectDisabled);
            var confirm = new DiscordButtonComponent(ButtonStyle.Success, "confirm", "Send Gift", buttonDisabled, new DiscordComponentEmoji("🎁"));
            
            message.ClearEmbeds();
            message.AddEmbed(embed);
            
            message.ClearComponents();
            message
                .AddComponents(select)
                .AddComponents(confirm);
        };
        
        rebuildComponents(false, true, 0);
        await context.RespondAsync(message);
        var response = await context.GetResponseAsync() ?? throw new Exception("Could not retrieve response");
        EventDropReply? selectedDrop = null;

        while (true)
        {
            var cancelSelect = new CancellationTokenSource();
            var cancelConfirm = new CancellationTokenSource();

            var expectConfirm = Task.Run(async () =>
            {
                var buttonResult = await context.Client.GetInteractivity()
                    .WaitForButtonAsync(response, context.User);
                if(cancelConfirm.IsCancellationRequested) return false;
                await cancelSelect.CancelAsync();
                
                if (buttonResult.TimedOut) return false;
                if (selectedDrop == null) throw new Exception("No drop selected.");
                
                var result = await inventoryClient.GiftEventCreditAsync(new GiftEventCreditMessage
                {
                    SenderLogin = member.Login,
                    RecipientLogin = receiverMember.Login,
                    Amount = (int)amount,
                    EventDropId = selectedDrop.Id
                });
                
                await buttonResult.Result.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithPalantirPresets(context)
                        .WithTitle("`🎉` Gift sent")
                        .WithDescription($"You have gifted {result.TotalAmount} {selectedDrop.Name} drops to {Formatter.Mention(receiver, true)}.\n" +
                                         $"Palantir has kept {result.LostAmount} drops of the gift.")));
                
                // exit loop
                return false;
            }, cancelConfirm.Token);
            
            var expectSelect = Task.Run(async () =>
            {
                var selectResult = await context.Client.GetInteractivity()
                    .WaitForSelectAsync(response, context.User, "dropId");
                if (cancelSelect.IsCancellationRequested) return false;
                await cancelConfirm.CancelAsync();
                
                if (selectResult.TimedOut) return false;
                
                await selectResult.Result.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);
                embed.ClearFields();

                selectedDrop = drops[Convert.ToInt32(selectResult.Result.Values.First())];
                embed.AddField("Selected Drop", $"`💧` {selectedDrop.Name}\n`💰` {credits[selectedDrop.Id].AvailableCredit} drops available\n_ _\n_ _ ");

                var validDrop = amount <= credits[selectedDrop.Id].AvailableCredit; 
                if (!validDrop)
                {
                    embed.AddField("`⚠️` Not enough credit", $"You do not have enough credit to gift the selected amount ({amount}).\nRe-run the command with a lesser amount.");
                }
                else
                {
                    embed.AddField("`✅` Confirm gift", $"You are about to gift {amount} drops.\n" +
                                                        "Confirm the gift by clicking the button below.");
                }

                rebuildComponents(false, !validDrop, selectedDrop.Id);
                await response.ModifyAsync(message);
                return true;
            }, cancelSelect.Token);

            // wait for a task to finish; filter out cancelled tasks
            Task<bool> finishedTask;
            do
            {
                var candidates = new List<Task<bool>> {expectSelect, expectConfirm}.Where(task => !task.IsCanceled).ToList();
                finishedTask = await Task.WhenAny(candidates);
            } while (finishedTask.IsCanceled); 
            if (!await finishedTask) break;
        }

        rebuildComponents(true, true, selectedDrop?.Id ?? 0);
        await response.ModifyAsync(message);
    }
    
    [Command("redeem"), TextAlias("rd"), RequirePalantirMember(MemberFlagMessage.Beta)]
    public async Task RedeemLeagueEventDrops(CommandContext context, uint amount, int eventSprite)
    {
        throw new NotImplementedException();
    }
}