using System.Text.RegularExpressions;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using tobeh.Palantir.Commands.Checks;
using tobeh.Palantir.Commands.Extensions;
using tobeh.TypoContentService;
using tobeh.Valmar;

namespace tobeh.Palantir.Commands.Commands;

/// <summary>
/// Tools for development and management
/// </summary>
[Command("manage")]
[TextAlias("mg")]
public class ManagementCommands(
    Members.MembersClient membersClient,
    MemberContext memberContext,
    StaticFiles.StaticFilesClient staticFilesClient,
    Sprites.SpritesClient spritesClient,
    Events.EventsClient eventsClient,
    Awards.AwardsClient awardsClient,
    Admin.AdminClient adminClient)
{
    /// <summary>
    /// Modify the flags of an user
    /// </summary>
    /// <param name="context"></param>
    [Command("flag")]
    [RequirePalantirMember(MemberFlagMessage.Moderator)]
    public async Task ModifyFlags(CommandContext context, ulong userId)
    {
        var flagOperator = memberContext.Member;
        MemberReply member =
            await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest { Id = (long)userId });

        List<MemberFlagMessage> flags = member.MappedFlags.ToList();
        MemberFlagMessage selectedFlag = MemberFlagMessage.PermaBan;
        var enumValues = Enum.GetValues<MemberFlagMessage>().ToList();

        var flagOrder = new[]
        {
            MemberFlagMessage.BubbleFarming, MemberFlagMessage.Admin, MemberFlagMessage.Moderator,
            MemberFlagMessage.UnlimitedCloud, MemberFlagMessage.Patron, MemberFlagMessage.PermaBan,
            MemberFlagMessage.DropBan, MemberFlagMessage.Patronizer, MemberFlagMessage.Booster, MemberFlagMessage.Beta,
            MemberFlagMessage.ContentModerator, MemberFlagMessage.EmojiManagement
        }.ToList();

        DiscordMessageBuilder BuildMessageFromFlags(bool disable = false)
        {
            var flagList = string.Join("\n",
                enumValues.Select(f => $"{(flags.Any(flag => flag == f) ? "`✔️`" : "`❌`")} {f}"));

            var msg = new DiscordMessageBuilder();
            msg.AddEmbed(new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithTitle($"Flags of {member.Username}")
                .WithDescription($"Flags manage the permissions of users in the typo ecosystem.\n" +
                                 $"To modify the flags, choose one in the dropdown and set its value.\n\n" +
                                 $"{flagList}")
            );

            var flagSelect = new DiscordSelectComponent(
                "flagSelect",
                "Select a flag",
                enumValues.Select(f =>
                    new DiscordSelectComponentOption(f.ToString(), f.ToString(), isDefault: selectedFlag == f)),
                disable);

            var currentFlagState = flags.Any(flag => flag == selectedFlag);
            var flagNotAllowed =
                selectedFlag is MemberFlagMessage.Admin or MemberFlagMessage.Moderator
                    or MemberFlagMessage.ContentModerator &&
                !flagOperator.MappedFlags.Contains(MemberFlagMessage.Admin);

            var flagStateSelect = new DiscordSelectComponent(
                "flagState",
                "Set the flag",
                new List<DiscordSelectComponentOption>
                {
                    new("✔️ Flag Active", "1", isDefault: currentFlagState),
                    new("❌ Flag Inactive", "0", isDefault: !currentFlagState)
                },
                disable || flagNotAllowed);

            msg.AddComponents(flagSelect);
            msg.AddComponents(flagStateSelect);
            return msg;
        }

        await context.RespondAsync(BuildMessageFromFlags());
        var response = await context.GetResponseAsync() ?? throw new NullReferenceException("no response");

        var selectedFlagHandler = new InteractivityHandler<bool>(
            async interactivity => await interactivity.WaitForSelectAsync(response, context.User, "flagSelect"),
            async result =>
            {
                await result.Result.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
                var target = result.Result.Values.First();
                selectedFlag = enumValues.First(val => val.ToString() == target);

                await response.ModifyAsync(BuildMessageFromFlags());
                return true;
            },
            false
        );

        var selectedStateHandler = new InteractivityHandler<bool>(
            async interactivity => await interactivity.WaitForSelectAsync(response, context.User, "flagState"),
            async result =>
            {
                await result.Result.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
                var target = result.Result.Values.First();
                var state = target == "1";
                var flagId = flagOrder.IndexOf(selectedFlag);

                await adminClient.UpdateMemberFlagsAsync(new UpdateMemberFlagsRequest
                {
                    FlagId = flagId,
                    State = state,
                    InvertOthers = false,
                    MemberIds = { member.DiscordId }
                });
                member = await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest
                    { Id = member.DiscordId });
                flags = member.MappedFlags.ToList();

                await response.ModifyAsync(BuildMessageFromFlags());
                return true;
            },
            false
        );

        while (true)
        {
            var continueInteractions = await context.Client.ServiceProvider.GetRequiredService<InteractivityExtension>()
                .HandleNextInteraction(
                [
                    selectedFlagHandler, selectedStateHandler
                ]);
            if (!continueInteractions) break;
        }

        await response.ModifyAsync(BuildMessageFromFlags(true));
    }

    /// <summary>
    /// Add a new sprite
    /// </summary>
    /// <param name="context"></param>
    /// <param name="name">The name of the sprite</param>
    /// <param name="price">The price in bubbles or event drops</param>
    /// <param name="rainbow">Whether the sprite is rainbow capable</param>
    /// <param name="artist">The artist name of the creator</param>
    /// <param name="eventDropId">Optional ID of the event drop, if the sprite is part of an event</param>
    /// <param name="sourceUrl">Optional URL of the sprite, if the sprite is not attached</param>
    [Command("newsprite")]
    [RequirePalantirMember(MemberFlagMessage.ContentModerator)]
    [TextAlias("ns")]
    public async Task AddNewSprite(CommandContext context, string name, uint price, bool rainbow, string? artist = null,
        uint? eventDropId = null, string? sourceUrl = null)
    {
        var safeName = Regex.Replace(name, "[^a-zA-Z0-9]", "_");
        var fileName = eventDropId is not null ? $"evd{eventDropId}-{safeName}" : $"{safeName}";
        var spriteUrl = eventDropId is not null
            ? $"https://static.typo.rip/sprites/event/{fileName}.gif"
            : $"https://static.typo.rip/sprites/regular/{fileName}.gif";

        string url;
        if (context is TextCommandContext { Message.Attachments.Count: > 0 } ctx)
        {
            url = ctx.Message.Attachments[0].Url ?? throw new NullReferenceException("Invalid attachment present");
        }
        else
        {
            url = sourceUrl ?? throw new NullReferenceException("No attachment present and no sprite url provided");
        }

        var response = await new HttpClient().GetAsync(url);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "image/gif")
            throw new InvalidOperationException("Invalid image");

        var sprite = await spritesClient.AddSpriteAsync(new AddSpriteMessage
        {
            Name = name,
            Cost = (int)price,
            Artist = artist,
            EventDropId = (int?)eventDropId,
            Url = spriteUrl,
            IsRainbow = rainbow
        });

        await staticFilesClient.AddFileFromBytes(await response.Content.ReadAsByteArrayAsync(), fileName, "gif",
            eventDropId is null ? FileType.Sprite : FileType.EventSprite);

        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"{sprite.Id.AsTypoId()} _ _ {sprite.Name}")
            .WithAuthor("Looking great!")
            .WithImageUrl(url)
            .WithDescription("This new sprite has been added!\n" +
                             $"You can view it with the command `/sprite view {sprite.Id}`"));
    }

    /// <summary>
    /// Add a new event
    /// </summary>
    /// <param name="context"></param>
    /// <param name="name"></param>
    [Command("newevent")]
    [RequirePalantirMember(MemberFlagMessage.ContentModerator)]
    [TextAlias("ne")]
    public async Task AddNewEvent(CommandContext context, string name, string description, int startInDays,
        int durationDays, bool progressive)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Invalid name", "The event name cannot be empty."));
            return;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "Invalid description", "The event description cannot be empty."));
            return;
        }

        var evt = await eventsClient.CreateEventAsync(new CreateEventMessage
        {
            Name = name,
            Description = description,
            StartInDays = startInDays,
            DurationDays = durationDays,
            Progressive = progressive
        });

        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"{evt.Id.AsTypoId()} _ _ {evt.Name}")
            .WithAuthor("The hype is real!")
            .WithDescription("This new event has been added!\n" +
                             $"You can view details using the command `/event view {evt.Id}`.\n" +
                             $"Event drops can be added with the command `/manage newdrop {evt.Id} <name> (url)`."));
    }

    /// <summary>
    /// Add a new event drop
    /// </summary>
    /// <param name="context"></param>
    /// <param name="eventId">The id of the associated event</param>
    /// <param name="name">The name of the new event drop</param>
    /// <param name="sourceUrl">The url of the sprite, if no attachment provided</param>
    [Command("newdrop")]
    [RequirePalantirMember(MemberFlagMessage.ContentModerator)]
    [TextAlias("nd")]
    public async Task AddNewEventDrop(CommandContext context, int eventId, string name, string? sourceUrl = null)
    {
        var safeName = Regex.Replace(name, "[^a-zA-Z0-9]", "_");
        var fileName = $"ev{eventId}-{safeName}";
        var dropurl = $"https://static.typo.rip/drops/{fileName}.gif";

        string url;
        if (context is TextCommandContext { Message.Attachments.Count: > 0 } ctx)
        {
            url = ctx.Message.Attachments[0].Url ?? throw new NullReferenceException("Invalid attachment present");
        }
        else
        {
            url = sourceUrl ?? throw new NullReferenceException("No attachment present and no drop url provided");
        }

        var response = await new HttpClient().GetAsync(url);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "image/gif")
            throw new InvalidOperationException("Invalid image");

        var drop = await eventsClient.CreateEventDropAsync(new CreateEventDropMessage()
        {
            Name = name,
            Url = dropurl,
            EventId = eventId
        });

        await staticFilesClient.AddFileFromBytes(await response.Content.ReadAsByteArrayAsync(), fileName, "gif",
            FileType.Drop);

        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"{drop.Id.AsTypoId()} _ _ {drop.Name}")
            .WithAuthor("Looking great!")
            .WithImageUrl(url)
            .WithDescription("This new event drop has been added!\n" +
                             $"You can add sprites to it by specifying the event drop ID in the `/manage newsprite` command."));
    }

    /// <summary>
    /// Add a new award
    /// </summary>
    /// <param name="context"></param>
    /// <param name="name">The name of the new award</param>
    /// <param name="rarity">The rarity (0=common, 3=legendary) of the award</param>
    /// <param name="sourceUrl">The url of the image, if no attachment provided</param>
    /// <param name="description">The award description text</param>
    [Command("newaward")]
    [RequirePalantirMember(MemberFlagMessage.ContentModerator)]
    [TextAlias("na")]
    public async Task AddNewAward(CommandContext context, string name, string description, AwardRarityMessage rarity,
        string? sourceUrl = null)
    {
        var safeName = Regex.Replace(name, "[^a-zA-Z0-9]", "_");
        var awardUrl = $"https://static.typo.rip/drops/{safeName}.gif";

        string url;
        if (context is TextCommandContext { Message.Attachments.Count: > 0 } ctx)
        {
            url = ctx.Message.Attachments[0].Url ?? throw new NullReferenceException("Invalid attachment present");
        }
        else
        {
            url = sourceUrl ?? throw new NullReferenceException("No attachment present and no award url provided");
        }

        var response = await new HttpClient().GetAsync(url);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "image/gif")
            throw new InvalidOperationException("Invalid image");

        var award = await awardsClient.CreateAwardAsync(new CreateAwardRequest()
        {
            Name = name,
            Url = awardUrl,
            Rarity = rarity,
            Description = description
        });

        await staticFilesClient.AddFileFromBytes(await response.Content.ReadAsByteArrayAsync(), safeName, "gif",
            FileType.Award);

        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"{award.Id.AsTypoId()} _ _ {award.Name} (Rarity: {award.Rarity})")
            .WithAuthor("Looking great!")
            .WithImageUrl(url)
            .WithDescription("This new award has been added!\n" +
                             $"> {award.Description}"));
    }
}