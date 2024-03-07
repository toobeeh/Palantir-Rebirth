namespace Palantir_Core.Discord;

public class DiscordApiClientOptions
{
    public required string DiscordToken { get; set; }
    public required ulong ServerId { get; set; }
    public required ulong BoostRoleId { get; set; }
    public required ulong BetaRoleId { get; set; }
}