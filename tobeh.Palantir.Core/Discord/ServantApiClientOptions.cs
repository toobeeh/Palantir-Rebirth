namespace tobeh.Palantir.Core.Discord;

public class ServantApiClientOptions : DiscordApiClientOptions
{
    public required ulong ServerId { get; set; }
    public required ulong BoostRoleId { get; set; }
    public required ulong BetaRoleId { get; set; }
}