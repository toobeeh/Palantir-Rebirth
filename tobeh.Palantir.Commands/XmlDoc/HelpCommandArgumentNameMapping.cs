using System.Collections.Frozen;
using DSharpPlus.Commands.Converters;

namespace tobeh.Palantir.Commands.XmlDoc;

public static class HelpCommandArgumentNameMapping
{
    public static readonly FrozenDictionary<Type, string> Names =
        new Dictionary<Type, string>
        {
            { typeof(UInt32Converter), "[uint] a positive number" },
            { typeof(UInt64Converter), "[ulong] a (large) positive number" },
            { typeof(DoubleConverter), "[double] a number with decimal places" },
            { typeof(DiscordUserConverter), "[user] a @mention of a discord user" },
            { typeof(StringConverter), "[string] a text of any length" },
            { typeof(DiscordEmojiConverter), "[emoji] any emoji that is supported by discord" },
            { typeof(EnumConverter), "[choice] a choice from a list of options" },
        }.ToFrozenDictionary();
}