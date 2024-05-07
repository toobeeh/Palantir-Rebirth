namespace tobeh.Palantir.Public.Discord;

public class DiscordBotClientOptions
{
    public required string DiscordToken { get; init; }
    public required string Prefix { get; init; }
    public required bool UseSlash { get; init; }
}