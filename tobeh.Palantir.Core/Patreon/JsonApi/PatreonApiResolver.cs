using Hypermedia.Configuration;
using Hypermedia.Metadata;
using tobeh.Palantir.Core.Patreon.JsonApi.Models;

namespace tobeh.Palantir.Core.Patreon.JsonApi;

public static class PatreonApiResolver
{
    public static IContractResolver CreateResolver()
    {
        return new Builder()
            .With<Member>("member")
            .Id(nameof(Member.id))
            .HasMany<Tier>(nameof(Member.currently_entitled_tiers))
            .BelongsTo<User>(nameof(Member.user))
            .With<Tier>("tier")
            .Id(nameof(Tier.id))
            .With<User>("user")
            .Id(nameof(User.id))
            .Build();
    }
}