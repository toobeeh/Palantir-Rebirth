using tobeh.Valmar;

namespace tobeh.Palantir.Lobbies.Util.GuildOptionsExtensions;

public static class GuildOptionsExtension
{
    public static bool HasSameOptions(this GuildOptionsMessage options, GuildOptionsMessage other)
        => options.GuildId == other.GuildId
           && options.ChannelId == other.ChannelId
           && options.Name == other.Name
           && options.Prefix == other.Prefix;
}