using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using Palantir_Commands.Services;
using Valmar;

namespace Palantir_Commands.Discord.Checks;

public class RequirePalantirMemberCheck(MemberContext memberContext, Members.MembersClient membersClient) : 
    IContextCheck<RequirePalantirMemberAttribute>,
    IContextCheck
{
    
    public async ValueTask<string?> ExecuteCheckAsync(
        RequirePalantirMemberAttribute attribute,
        CommandContext context)
    {
        try
        {
            var member = await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest
                { Id = (long)context.User.Id });
            memberContext.Member = member;
        }
        catch
        {
            return null; // "This command requires you to have a Palantir account.";
        }

        var firstMissingFlag =
            attribute.RequiredFlags.FindIndex(flag => !memberContext.Member.MappedFlags.Contains(flag));
        
        return firstMissingFlag >= 0 ? $"You need the flag {attribute.RequiredFlags[firstMissingFlag]} to use this command." : null;
    }
}