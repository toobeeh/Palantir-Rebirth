namespace Palantir_Core.Patreon.Models;

public class User
{
    public required string id { get; set; }
    public SocialConnections social_connections { get; set; }
}

public class SocialConnections
{
    public string discordId { get; set; }
}

