namespace Palantir_Commands.Discord.Extensions;

public static class PalantirFormatter
{
    public static string AsTypoId(this int id) => $"`🆔 {id}`";
    public static string AsTypoLink(this string name, string url, string emote) => $"[`{emote} {name}`]({url})";
}