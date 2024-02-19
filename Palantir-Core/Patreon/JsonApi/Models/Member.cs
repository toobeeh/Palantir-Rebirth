namespace Palantir_Core.Patreon.Models;

public class Member
{
    public required string id { get; set; }
    public required string full_name { get; set; }
    public required IReadOnlyList<Tier> currently_entitled_tiers { get; set; }
    public required User user { get; set; }
}