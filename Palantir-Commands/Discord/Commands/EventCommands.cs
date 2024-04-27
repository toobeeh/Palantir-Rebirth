using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Palantir_Commands.Discord.Checks;
using Palantir_Commands.Discord.Extensions;
using Palantir_Commands.Services;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace Palantir_Commands.Discord.Commands;

/// <summary>
/// View your current event stats or browse past events
/// </summary>
/// <param name="logger"></param>
/// <param name="eventsClient"></param>
/// <param name="inventoryClient"></param>
/// <param name="spritesClient"></param>
/// <param name="scenesClient"></param>
/// <param name="statsClient"></param>
/// <param name="membersClient"></param>
/// <param name="memberContext"></param>
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
    /// <summary>
    /// Show a list of all events that have ever happened
    /// </summary>
    /// <param name="context"></param>
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
        var pages = list.Chunk(pageSize).Select(page =>
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

    /// <summary>
    /// View a summary of your current event progress
    /// </summary>
    /// <param name="context"></param>
    /// <param name="eventId">The ID of the event that will be shown</param>
    [Command("view"), TextAlias("vw"), DefaultGroupCommand, RequirePalantirMember]
    public async Task ViewEvent(CommandContext context, uint? eventId = null)
    {
        logger.LogTrace("ViewEvent(eventId={eventId})", eventId);

        var member = memberContext.Member;
        var evt = eventId is { } eventIdValue
            ? await eventsClient.GetEventByIdAsync(new GetEventRequest { Id = (int)eventIdValue })
            : await eventsClient.GetCurrentEventAsync(new Empty());

        var credit = await inventoryClient
            .GetEventCredit(new GetEventCreditRequest { Login = member.Login, EventId = evt.Id })
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
                Description = group.Drop.ReleaseStart.ToDateTimeOffset() < DateTimeOffset.UtcNow
                    ? $"```js\n💧 {group.Drop.Name} Drop```\n  " +
                      $"> **Drop Credit:**\n>  {group.Credit.TotalCredit} collected, {group.Credit.AvailableCredit} available\n>   {(evt.Progressive ? group.Credit.RedeemableAmount : totalRedeemable):0.#} redeemable\n> **Sprites**:\n" +
                      $"{string.Join("\n", group.Sprites.Select(sprite =>
                          $"> {(spriteInventory.Any(slot => slot.SpriteId == sprite.Id) ? "`📦`" : "")} {sprite.Id.AsTypoId()} {sprite.Name}: {sprite.Cost} Drops"))}"
                    : $"This drop will release on {Formatter.Timestamp(group.Drop.ReleaseStart.ToDateTimeOffset(), TimestampFormat.LongDate)}.\nStay tuned!"
            }).ToList();


        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor("Viewing your event progress")
            .WithTitle($"{evt.Id.AsTypoId()} {evt.Name} Event")
            .WithDescription(
                $"From {Formatter.Timestamp(evt.StartDate.ToDateTimeOffset(), TimestampFormat.LongDate)} to {Formatter.Timestamp(evt.EndDate.ToDateTimeOffset(), TimestampFormat.LongDate)} ({evt.Length} days)\n" +
                (evt.Progressive ? "This event is progressive. Event drops and sprites will reveal gradually!" : "") +
                (giftLossRate.CollectedDrops > 0
                    ? $"Your current gift loss rate is `{giftLossRate.LossRateBase * 100:0.#}%` ({giftLossRate.CollectedDrops:0.#}/{giftLossRate.RequiredDrops} drops)\nTo gift event drops, use `/event gift (@receiver)`\n"
                    : "") +
                $"\n```\n{evt.Description}```");

        try
        {
            var scene = await scenesClient.GetEventSceneAsync(new GetEventSceneRequest { EventId = evt.Id });
            var traceStartDate = Timestamp.FromDateTimeOffset(evt.StartDate.ToDateTimeOffset().AddDays(-1));
            var bubbleRange = await statsClient.GetBubbleTimespanRangeAsync(new BubbleTimespanRangeRequest
                { Login = member.Login, StartDate = traceStartDate, EndDate = evt.EndDate });
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

    /// <summary>
    /// Gift event drops to another member
    /// </summary>
    /// <param name="context"></param>
    /// <param name="receiver">The member that will receive your gift</param>
    /// <exception cref="Exception"></exception>
    [Command("gift"), TextAlias("gf"), RequirePalantirMember(MemberFlagMessage.Beta)]
    public async Task GiftEventDrops(CommandContext context, DiscordMember receiver)
    {
        logger.LogTrace("GiftEventDrops(receiver={receiver})", receiver);

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

        var events = await eventsClient.GetAllEvents(new Empty()).ToDictionaryAsync(e => e.Id);
        var selectedEvent = events.MaxBy(kv => kv.Key).Value;
        var credits = await inventoryClient
            .GetEventCredit(new GetEventCreditRequest { Login = member.Login, EventId = selectedEvent.Id })
            .ToDictionaryAsync(credit => credit.EventDropId);
        var drops = await eventsClient.GetEventDropsOfEvent(new GetEventRequest { Id = selectedEvent.Id })
            .ToDictionaryAsync(drop => drop.Id);
        var lossRate = await inventoryClient.GetGiftLossRateAsync(new GetGiftLossRateMessage
            { EventId = selectedEvent.Id, Login = member.Login });


        EventDropReply? selectedDrop = null;
        var selectedAmount = 0;

        DiscordMessageBuilder BuildMessageFromState(bool disableAll = false)
        {
            var message = new DiscordMessageBuilder();

            var dropSelectDisabled = false;
            var amountSelectDisabled = false;
            var buttonDisabled = false;
            var embed = new DiscordEmbedBuilder().WithPalantirPresets(context)
                .WithAuthor($"Gift {(selectedAmount == 0 ? "" : selectedAmount)} event drops")
                .WithTitle($"{selectedEvent.Id.AsTypoId()} {selectedEvent.Name} Event")
                .WithDescription($"Using gifts, you can transfer some of your event drops to a friend.\n" +
                                 $"When you do that, Palantir will keep a small amount of the gift to prevent shady business.\n" +
                                 $"Your current gift loss rate is `{lossRate.LossRateBase * 100:0.#}%` ({lossRate.CollectedDrops:0.#}/{lossRate.RequiredDrops} drops)\n_ _\n" +
                                 $"To send a gift, choose the event drop which you want to gift to {Formatter.Mention(receiver, true)}.");


            if (selectedDrop is not null)
            {
                embed.AddField("Selected Drop",
                    $"`💧` {selectedDrop.Name}\n`💰` {credits[selectedDrop.Id].AvailableCredit} drops available\n_ _\n_ _ ");

                if (selectedAmount > 0)
                {
                    var validDrop = selectedAmount <= credits[selectedDrop.Id].AvailableCredit;
                    if (!validDrop)
                    {
                        buttonDisabled = true;
                        embed.AddField("`⚠️` Not enough credit",
                            $"You do not have enough credit to gift the selected amount ({selectedAmount}).\nChoose a lesser amount.");
                    }
                    else
                    {
                        embed.AddField("`✅` Confirm gift", $"You are about to gift {selectedAmount} drops.\n" +
                                                           "Confirm the gift by clicking the button below.");
                    }
                }
            }

            var dropOptions = drops.Values.OrderByDescending(drop => drop.Id)
                .Select(drop => new DiscordSelectComponentOption(drop.Name, drop.Id.ToString(),
                    $"{credits[drop.Id].AvailableCredit} drops available", selectedDrop?.Id == drop.Id))
                .ToList();

            var eventOptions = events.Values.OrderByDescending(selectEvent => selectEvent.Id)
                .Select(evt =>
                    new DiscordSelectComponentOption(evt.Name + " Event", evt.Id.ToString(), $" ",
                        evt.Id == selectedEvent.Id))
                .ToList();

            var creditOptions = Enumerable
                .Repeat(1, selectedDrop is not null ? Math.Min(25, credits[selectedDrop.Id].AvailableCredit) : 0)
                .Select((_, idx) =>
                    new DiscordSelectComponentOption($"{idx + 1} Drops", (idx + 1).ToString(), $" ",
                        selectedAmount == idx + 1))
                .ToList();

            if (creditOptions.Count == 0)
            {
                amountSelectDisabled = true;
                creditOptions.Add(new DiscordSelectComponentOption($"- Drops", "value", $" ", false));
            }

            var eventSelect = new DiscordSelectComponent("eventId", "Choose an event", eventOptions, disableAll);
            var dropSelect = new DiscordSelectComponent("dropId", "Choose an event drop", dropOptions,
                disableAll || dropSelectDisabled);
            var amountSelect = new DiscordSelectComponent("amount", "Choose the gift amount", creditOptions,
                disableAll || amountSelectDisabled || dropSelectDisabled || selectedDrop is null);
            var confirm = new DiscordButtonComponent(DiscordButtonStyle.Success, "confirm", "Send Gift",
                selectedAmount == 0 || disableAll || buttonDisabled, new DiscordComponentEmoji("🎁"));

            message.ClearEmbeds();
            message.AddEmbed(embed);

            message.ClearComponents();
            message.AddComponents(eventSelect)
                .AddComponents(dropSelect)
                .AddComponents(amountSelect)
                .AddComponents(confirm);

            return message;
        }

        await context.RespondAsync(BuildMessageFromState());
        var response = await context.GetResponseAsync() ?? throw new Exception("Could not retrieve response");

        var confirmInteractionHandler = new InteractivityHandler<bool>(
            async interactivity => await interactivity.WaitForButtonAsync(response, context.User),
            async result =>
            {
                if (selectedDrop == null) throw new Exception("No drop selected.");

                var giftResult = await inventoryClient.GiftEventCreditAsync(new GiftEventCreditMessage
                {
                    SenderLogin = member.Login,
                    RecipientLogin = receiverMember.Login,
                    Amount = selectedAmount,
                    EventDropId = selectedDrop.Id
                });

                await result.Result.Interaction.CreateResponseAsync(
                    DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithPalantirPresets(context)
                            .WithTitle("`🎉` Gift sent")
                            .WithDescription(
                                $"You have gifted {giftResult.TotalAmount} {selectedDrop.Name} drops to {Formatter.Mention(receiver, true)}.\n" +
                                $"Palantir has kept {giftResult.LostAmount} drops of the gift.")));

                return false;
            },
            false
        );

        var dropSelectInteractionHandler = new InteractivityHandler<bool>(
            async interactivity => await interactivity.WaitForSelectAsync(response, context.User, "dropId"),
            async result =>
            {
                await result.Result.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
                selectedDrop = drops[Convert.ToInt32(result.Result.Values.First())];

                await response.ModifyAsync(BuildMessageFromState());
                return true;
            },
            false
        );

        var eventSelectInteractionHandler = new InteractivityHandler<bool>(
            async interactivity => await interactivity.WaitForSelectAsync(response, context.User, "eventId"),
            async result =>
            {
                await result.Result.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
                selectedEvent = events[Convert.ToInt32(result.Result.Values.First())];
                credits = await inventoryClient
                    .GetEventCredit(new GetEventCreditRequest { Login = member.Login, EventId = selectedEvent.Id })
                    .ToDictionaryAsync(credit => credit.EventDropId);
                drops = await eventsClient.GetEventDropsOfEvent(new GetEventRequest { Id = selectedEvent.Id })
                    .ToDictionaryAsync(drop => drop.Id);
                lossRate = await inventoryClient.GetGiftLossRateAsync(new GetGiftLossRateMessage
                    { EventId = selectedEvent.Id, Login = member.Login });
                selectedDrop = null;
                selectedAmount = 0;

                await response.ModifyAsync(BuildMessageFromState());
                return true;
            },
            false
        );

        var amountSelectInteractionHandler = new InteractivityHandler<bool>(
            async interactivity => await interactivity.WaitForSelectAsync(response, context.User, "amount"),
            async result =>
            {
                await result.Result.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
                selectedAmount = Convert.ToInt32(result.Result.Values.First());

                await response.ModifyAsync(BuildMessageFromState());
                return true;
            },
            false
        );

        while (true)
        {
            var continueInteractions = await context.Client.GetInteractivity().HandleNextInteraction(
            [
                confirmInteractionHandler, dropSelectInteractionHandler, eventSelectInteractionHandler,
                amountSelectInteractionHandler
            ]);
            if (!continueInteractions) break;
        }

        await response.ModifyAsync(BuildMessageFromState(true));
    }
}