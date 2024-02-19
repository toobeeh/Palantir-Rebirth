namespace Palantir_Core.Patreon.Models;

public class Member
{
    public String id { get; set; }
    public String full_name { get; set; }
    public IReadOnlyList<Tier> currently_entitled_tiers { get; set; }
    public User user { get; set; }
}