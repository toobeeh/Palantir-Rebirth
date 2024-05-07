namespace tobeh.Palantir.Core.Patreon;

public record PatreonSubscriptions(IReadOnlyList<long> Patronizer, IReadOnlyList<long> Patrons);