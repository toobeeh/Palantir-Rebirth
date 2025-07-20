using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using Grpc.Core;
using tobeh.Valmar;

namespace tobeh.Palantir.Commands.Checks;

public class RequirePalantirMemberCheck(MemberContext memberContext, Members.MembersClient membersClient) :
    IContextCheck<RequirePalantirMemberAttribute>,
    IContextCheck
{
    public async ValueTask<string?> ExecuteCheckAsync(
        RequirePalantirMemberAttribute attribute,
        CommandContext context)
    {
        MemberReply member;

        try
        {
            // attach member to context, if not already by parent check
            if (!memberContext.HasMemberAssigned)
            {
                member = await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest
                    { Id = (long)context.User.Id });
                memberContext.Member = member;
            }
            else member = memberContext.Member;
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
        {
            return "**Server maintenance!** Please try again in a few minutes.";
        }
        catch
        {
            return "This command requires you to have a Palantir account.";
        }

        var firstMissingFlag =
            attribute.RequiredFlags.FindIndex(flag => !member.MappedFlags.Contains(flag));

        return firstMissingFlag >= 0
            ? $"You need the role '{attribute.RequiredFlags[firstMissingFlag]}' to use this command."
            : null;
    }
}