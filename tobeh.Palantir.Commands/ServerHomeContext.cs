using tobeh.Valmar;

namespace tobeh.Palantir.Commands;

public class ServerHomeContext
{
    private GuildOptionsMessage? _server;

    public bool HasServerAssigned => _server is not null;

    public GuildOptionsMessage Server
    {
        get
        {
            if (_server is null) throw new NullReferenceException("Server context did not register any server");
            return _server;
        }
        set
        {
            if (_server is not null) throw new InvalidOperationException("Server context already registered a server");
            _server = value;
        }
    }
}