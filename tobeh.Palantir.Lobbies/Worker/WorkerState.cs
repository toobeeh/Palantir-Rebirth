using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using tobeh.Palantir.Lobbies.Discord;
using tobeh.Valmar;

namespace tobeh.Palantir.Lobbies.Worker;

public record InstanceClaim(InstanceDetailsMessage InstanceDetails, Ulid Claim);

public record GuildAssignment(GuildOptionsMessage GuildOptions, IHost DiscordBotHost);

public class WorkerState(
    DiscordBotHostFactory discordBotHostFactory,
    ILogger<WorkerState> logger,
    IOptions<DiscordOptions> discordOptions
)
{
    private readonly Ulid _workerUlid = Ulid.NewUlid();

    public Ulid WorkerUlid => _workerUlid;
    public InstanceClaim? Instance { get; private set; }
    public GuildOptionsMessage? GuildOptions { get; private set; }
    public GuildAssignment? GuildAssignment { get; private set; }

    public SemaphoreSlim ReclaimSemaphore { get; } = new(1);
    public SemaphoreSlim DiscordRecreateSemaphore { get; } = new(1);

    public InstanceClaim AssignInstance(InstanceDetailsMessage instanceDetails, Ulid claim)
    {
        if (instanceDetails.Id != Instance?.InstanceDetails.Id)
            logger.LogInformation("Assigned instance {instanceDetails.Id}", instanceDetails.Id);
        Instance = new InstanceClaim(instanceDetails, claim);
        return Instance;
    }

    public async Task<GuildAssignment> AssignGuild(GuildOptionsMessage guildOptions, string botToken)
    {
        logger.LogTrace("AssignGuild({guildOptions}, {botToken})", guildOptions, botToken);

        // if a client is already assigned, dispose it
        if (GuildAssignment is not null)
        {
            await GuildAssignment.DiscordBotHost.StopAsync();
            GuildAssignment.DiscordBotHost.Dispose();
            GuildAssignment = null;
        }

        // set new guild options
        GuildOptions = guildOptions;

        // create new host which contains the discord bot
        var host = discordBotHostFactory.CreateBotHost(botToken, guildOptions.Prefix, builder =>
        {
            // leave all guilds except the assigned one
            builder.HandleGuildDownloadCompleted(async (c, args) =>
            {
                foreach (var guild in args.Guilds.Values)
                {
                    if (discordOptions.Value.WhitelistedServers.Contains((long)guild.Id)) continue;
                    if (guild.Id != (ulong)guildOptions.GuildId)
                    {
                        logger.LogInformation($"Leaving guild {guild.Name}");
                        await guild.LeaveAsync();
                    }
                    else
                    {
                        await guild.CurrentMember.ModifyAsync(member =>
                            member.Nickname = guildOptions.BotName ?? $"{GuildOptions.Name} Lobbies");
                    }
                }
            });

            // listen to guild join event and leave if its the wrong guild, or set nickname accordingly
            builder.HandleGuildCreated(async (c, args) =>
            {
                if (discordOptions.Value.WhitelistedServers.Contains((long)args.Guild.Id)) return;
                if (args.Guild.Id != (ulong)guildOptions.GuildId)
                {
                    await args.Guild.LeaveAsync();
                }
                else
                {
                    await args.Guild.CurrentMember.ModifyAsync(member =>
                        member.Nickname = guildOptions.BotName ?? $"{GuildOptions.Name} Lobbies");
                }
            });
        });

        await host.StartAsync();

        GuildAssignment = new GuildAssignment(guildOptions, host);
        return GuildAssignment;
    }
}