namespace tobeh.Palantir.Core.Patreon;

public class PatreonApiClientOptions
{
    public required string CreatorAccessToken { get; set; }
    public required string PatronTierId { get; set; }
    public required string PatronizerTierId { get; set; }
    public required long[] AdditionalPatronDiscordIds { get; set; } = [];
    public required long[] AdditionalPatronizerDiscordIds { get; set; } = [];
}