using Microsoft.Extensions.Logging;
using Valmar;

namespace Palantir_Commands.Services;

public class MemberContext
{
    private static int i = 0;

    public MemberContext(ILogger<MemberContext> logger)
    {
        logger.LogDebug("Context #{i} created", i++);
    }
    
    private MemberReply? _member;
    
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