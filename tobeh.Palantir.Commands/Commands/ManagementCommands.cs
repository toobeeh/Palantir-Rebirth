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
            MemberFlagMessage.ContentModerator
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
    /// Modify the flags of an user
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

        var sprite = await spritesClient.AddSpriteAsync(new AddSpriteMessage
        {
            Name = name,
            Cost = (int)price,
            Artist = artist,
            EventDropId = (int?)eventDropId,
            Url = spriteUrl,
            IsRainbow = rainbow
        });

        var response = await new HttpClient().GetAsync(url);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "image/gif")
            throw new InvalidOperationException("Invalid image");

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
}