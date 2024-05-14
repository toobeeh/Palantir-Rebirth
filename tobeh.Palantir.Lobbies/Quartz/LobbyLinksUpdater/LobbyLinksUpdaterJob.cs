using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Quartz;
using tobeh.Palantir.Lobbies.Quartz.DiscordLobbyUpdater;
using tobeh.Palantir.Lobbies.Util;
using tobeh.Palantir.Lobbies.Worker;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Lobbies.Quartz.LobbyLinksUpdater;

public class LobbyLinksUpdaterJob(
    WorkerState workerState,
    Valmar.Lobbies.LobbiesClient lobbiesClient,
    Members.MembersClient membersClient,
    WorkerService workerService,
    ILogger<DiscordLobbyUpdaterJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        var instance = await workerService.ReclaimInstance();
        var guildAssignment = await workerService.GetAssignedGuild(instance);
        var guildOptions = guildAssignment.GuildOptions;

        // update lobby links, if guild has enabled
        if (guildOptions.ChannelId is not null)
        {
            var lobbies = await lobbiesClient.GetCurrentLobbies(new Empty()).ToListAsync();
            var memberLogins = lobbies.SelectMany(lobby => lobby.Players.Select(player => player.Login));
            var memberDetails = await
                membersClient.GetMembersByLogin(new GetMembersByLoginMessage { Logins = { memberLogins } })
                    .ToListAsync();

            // set guild lobby links
            var links = LobbyMessageUtil.BuildGuildLinks(lobbies, memberDetails, guildOptions.GuildId,
                guildOptions.Invite);
            await lobbiesClient.SetGuildLobbyLinksAsync(new SetGuildLobbyLinksMessage
                { GuildId = guildOptions.GuildId, Links = { links } });

            logger.LogInformation("Updated lobby links for server {guildId} in instance {instance}",
                guildOptions.GuildId, instance.InstanceDetails.Id);
        }
        else
        {
            logger.LogInformation("No lobbies channel set for server {guildId} in instance {instance}",
                guildOptions.GuildId, instance.InstanceDetails.Id);
        }
    }
}