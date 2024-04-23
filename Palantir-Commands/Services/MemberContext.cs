using tobeh.Valmar;

namespace Palantir_Commands.Services;

/// <summary>
/// Wrapper class to be injected in command service scope.
/// Can be attached a member to be used later throughout command execution.
/// </summary>
public class MemberContext
{
    private MemberReply? _member;
    
    public bool HasMemberAssigned => _member is not null;
    
    public MemberReply Member { get
        {
            if (_member is null) throw new NullReferenceException("Member context did not register any member");
            return _member;
        } set
        {
            if (_member is not null) throw new InvalidOperationException("Member context already registered a member");
            _member = value;
        }
    }
}