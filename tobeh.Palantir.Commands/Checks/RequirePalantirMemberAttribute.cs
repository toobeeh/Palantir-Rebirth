using DSharpPlus.Commands.ContextChecks;
using tobeh.Valmar;

namespace tobeh.Palantir.Commands.Checks;

public class RequirePalantirMemberAttribute : ContextCheckAttribute
{
    public RequirePalantirMemberAttribute() : base()
    {
        RequiredFlags = [];
    }

    public RequirePalantirMemberAttribute(params MemberFlagMessage[] requiredFlags) : base()
    {
        RequiredFlags = requiredFlags.ToList();
    }

    public List<MemberFlagMessage> RequiredFlags { get; private set; }
}