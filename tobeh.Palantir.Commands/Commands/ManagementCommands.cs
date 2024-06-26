using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
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
    Members.MembersClient membersClient)
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

        DiscordMessageBuilder BuildMessageFromFlags()
        {
            var enumValues = Enum.GetValues<MemberFlagMessage>();
            var flagList = string.Join("\n",
                enumValues.Select(f => $"{f}: {(flags.Any(flag => flag == f) ? "`✔️`" : "`❌`")}"));

            var msg = new DiscordMessageBuilder();
            msg.AddEmbed(new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithTitle($"Flags of {member.Username}")
                .WithDescription(flagList)
            );

            return msg;
        }

        ;

        await context.RespondAsync(BuildMessageFromFlags());
    }
}