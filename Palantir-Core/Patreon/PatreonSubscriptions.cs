namespace Palantir_Core.Patreon;

public record PatreonSubscriptions(IReadOnlyList<ulong> Patronizer, IReadOnlyList<ulong> Patrons);