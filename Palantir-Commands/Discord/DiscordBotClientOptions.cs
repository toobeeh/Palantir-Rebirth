namespace Palantir_Commands.Discord;

public class DiscordBotClientOptions
{
    public required string DiscordToken { get; init; }
    public required string Prefix { get; init; }
}