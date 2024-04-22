namespace Palantir_Commands.Discord.Extensions;

public static class PalantirFormatter
{
    public static string AsTypoId(this int id) => $"`🆔 {id}`";
    public static string AsTypoLink(this string name, string url, string emote) => $"[`{emote} {name}`]({url})";
    public static string AsCountdownTimespan(this TimeSpan time) {
        string result = string.Empty;

        if (time.Days > 0)
            result += $"{time.Days}{("d")}, ";
        
        if (time.Hours > 0)
            result += $"{time.Hours}{("h")}, ";
        
        if (time.Minutes > 0)
            result += $"{time.Minutes}{("min")}, ";
        
        if (time.Seconds > 0)
            result += $"{time.Seconds}{("s")}";

        if (result.EndsWith(", "))
            result = result.Remove(result.LastIndexOf(",", StringComparison.Ordinal));

        return result;
    }
}