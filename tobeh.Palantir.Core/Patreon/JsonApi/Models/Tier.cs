namespace tobeh.Palantir.Core.Patreon.JsonApi.Models;

public class Tier
{
    public required string id { get; set; }
    public required string title { get; set; }
    public required string[] discord_role_ids { get; set; }
}