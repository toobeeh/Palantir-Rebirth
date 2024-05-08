using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Quartz;
using tobeh.Palantir.Lobbies.Util;
using tobeh.Palantir.Lobbies.Util.GuildOptionsExtensions;
using tobeh.Palantir.Lobbies.Worker;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Lobbies.Quartz.WorkerLobbyUpdater;

public class WorkerLobbyUpdaterJob(
    WorkerState workerState,
    Workers.WorkersClient workersClient,
    Valmar.Lobbies.LobbiesClient lobbiesClient,
    Members.MembersClient membersClient,
    ILogger<WorkerLobbyUpdaterJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        var instance = workerState.Instance;
        logger.LogInformation("Instance {id} triggered refresh", workerState.WorkerUlid.ToString());

        // claim a new instance if none set
        if (instance is null)
        {
            InstanceDetailsMessage unclaimedInstance;
            try
            {
                unclaimedInstance = await workersClient.GetUnclaimedInstanceAsync(new Empty());
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
            {
                logger.LogInformation("No unclaimed instance available");
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to fetch unclaimed instance");
                return;
            }

            var claim = Ulid.NewUlid();
            var confirmedInstance = await workersClient.ClaimInstanceAsync(new ClaimInstanceMessage
            {
                WorkerUlid = workerState.WorkerUlid.ToString(),
                InstanceId = unclaimedInstance.Id,
                ClaimUlid = claim.ToString()
            });

            instance = workerState.AssignInstance(confirmedInstance, claim);
        }

        // re-claim instance to keep claim
        else
        {
            var newClaim = Ulid.NewUlid();
            InstanceDetailsMessage updatedInstance;
            try
            {
                updatedInstance = await workersClient.ClaimInstanceAsync(new ClaimInstanceMessage
                {
                    WorkerUlid = workerState.WorkerUlid.ToString(),
                    InstanceId = instance.InstanceDetails.Id,
                    ClaimUlid = newClaim.ToString(),
                    LastClaimUlid = instance.Claim.ToString()
                });
            }
            catch (Exception e)
            {
                logger.LogDebug(e, "Failed to re-claim instance {instanceId}", instance.InstanceDetails.Id);
                return;
            }

            instance = workerState.AssignInstance(updatedInstance, newClaim);
        }

        // get guild options for instance, throws if none set
        GuildOptionsMessage guildOptions;
        try
        {
            guildOptions = await workersClient.GetAssignedGuildOptionsAsync(new GetAssignedGuildOptionsMessage
            {
                InstanceId = instance.InstanceDetails.Id
            });
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        {
            logger.LogInformation("No guild options set for instance {instanceId}", instance.InstanceDetails.Id);
            return;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get guild options for instance {instanceId}", instance.InstanceDetails.Id);
            return;
        }

        // get discord client, create if not exists or settings changed
        var guildAssignment = workerState.GuildAssignment;
        if (guildAssignment is null || !guildAssignment.GuildOptions.HasSameOptions(guildOptions))
        {
            guildAssignment = await workerState.AssignGuild(guildOptions, instance.InstanceDetails.BotToken);
        }

        // build lobbies, if enabled
        var channelId = guildAssignment.GuildOptions.ChannelId;
        if (channelId is not null)
        {
            var channel = await guildAssignment.BotClient.GetChannelAsync((ulong)channelId);

            var lobbies = await lobbiesClient.GetCurrentLobbies(new Empty()).ToListAsync();
            var memberLogins = lobbies.SelectMany(lobby => lobby.Players.Select(player => player.Login));
            var memberDetails = await
                membersClient.GetMembersByLogin(new GetMembersByLoginMessage { Logins = { memberLogins } })
                    .ToListAsync();

            var header = LobbyMessageUtil.BuildHeader(guildAssignment.GuildOptions.Invite);
            var lobbiesContent =
                LobbyMessageUtil.BuildLobbies(lobbies, memberDetails, guildOptions.GuildId, guildOptions.Invite);
            var availableMessages = await LobbyMessageUtil.GetMessageCandidatesInChannel(
                channel, guildAssignment.BotClient.CurrentUser.Id);
            var splits = LobbyMessageUtil.SplitContentToMessages(availableMessages, header, lobbiesContent);

            foreach (var split in splits)
            {
                if (split.Message is null)
                {
                    await channel.SendMessageAsync(split.Content);
                }
                else
                {
                    if (split.Message.Content != split.Content) await split.Message.ModifyAsync(split.Content);
                }
            }
        }

        logger.LogInformation("Finished");
    }
}