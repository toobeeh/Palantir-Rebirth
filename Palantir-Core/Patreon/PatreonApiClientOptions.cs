namespace Palantir_Core.Patreon;

public class PatreonApiClientOptions
{
    public required string CreatorAccessToken { get; set; }
    public required string PatronTierId { get; set; }
    public required string PatronizerTierId { get; set; }
}