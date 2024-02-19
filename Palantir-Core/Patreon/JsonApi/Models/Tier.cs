namespace Palantir_Core.Patreon.Models;

public class Tier
{
    public required string id { get; set; }
    public required string title { get; set; }
    public required string[] discord_role_ids { get; set; }
}