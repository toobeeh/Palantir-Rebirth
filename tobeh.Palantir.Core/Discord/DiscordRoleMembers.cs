namespace tobeh.Palantir.Core.Discord;

public record DiscordRoleMembers(IReadOnlyList<long> BetaMembers, IReadOnlyList<long> BoostMembers);