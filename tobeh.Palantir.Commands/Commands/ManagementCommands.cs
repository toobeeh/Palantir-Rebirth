using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using tobeh.Palantir.Commands.Checks;
using tobeh.Palantir.Commands.Extensions;
using tobeh.Valmar;

namespace tobeh.Palantir.Commands.Commands;

/// <summary>
/// Tools for development and management
/// </summary>
[Command("manage")]
[TextAlias("mg")]
[RequirePalantirMember(MemberFlagMessage.Admin)]
public class ManagementCommands(
    Members.MembersClient membersClient,
    Admin.AdminClient adminClient)
{
    /// <summary>
    /// Modify the flags of an user
    /// </summary>
    /// <param name="context"></param>
    [Command("flag")]
    public async Task ResetCommands(CommandContext context, ulong userId)
    {
        MemberReply member =
            await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest { Id = (long)userId });

        List<MemberFlagMessage> flags = member.MappedFlags.ToList();
        MemberFlagMessage selectedFlag = MemberFlagMessage.PermaBan;
        var enumValues = Enum.GetValues<MemberFlagMessage>().ToList();

        var flagOrder = new[]
        {
            MemberFlagMessage.BubbleFarming, MemberFlagMessage.Admin, MemberFlagMessage.Moderator,
            MemberFlagMessage.UnlimitedCloud, MemberFlagMessage.Patron, MemberFlagMessage.PermaBan,
            MemberFlagMessage.DropBan, MemberFlagMessage.Patronizer, MemberFlagMessage.Booster, MemberFlagMessage.Beta
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

            var flagStateSelect = new DiscordSelectComponent(
                "flagState",
                "Set the flag",
                new List<DiscordSelectComponentOption>
                {
                    new("✔️ Flag Active", "1", isDefault: currentFlagState),
                    new("❌ Flag Inactive", "0", isDefault: !currentFlagState)
                },
                disable);

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
            var continueInteractions = await context.Client.GetInteractivity().HandleNextInteraction(
            [
                selectedFlagHandler, selectedStateHandler
            ]);
            if (!continueInteractions) break;
        }

        await response.ModifyAsync(BuildMessageFromFlags(true));
    }
}