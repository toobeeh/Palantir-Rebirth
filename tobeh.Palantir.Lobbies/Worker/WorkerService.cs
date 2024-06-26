using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using tobeh.Palantir.Lobbies.Util.GuildOptionsExtensions;
using tobeh.Valmar;

namespace tobeh.Palantir.Lobbies.Worker;

public class WorkerService(
    ILogger<WorkerService> logger,
    WorkerState workerState,
    IOptions<WorkerOptions> workerOptions,
    Workers.WorkersClient workersClient)
{
    private readonly SemaphoreSlim _reclaimSemaphore = new(1);

    public async Task<InstanceClaim> ReclaimInstance()
    {
        logger.LogTrace("ReclaimInstance()");

        await _reclaimSemaphore.WaitAsync();

        var instance = workerState.Instance;

        // claim a new instance if none set
        if (instance is null)
        {
            int targetInstanceId;

            if (workerOptions.Value.ForceBotInstanceId is { } forcedId) // if forced, use it
            {
                logger.LogInformation("Forced instance #{forcedId}", forcedId);
                targetInstanceId = forcedId;
            }
            else // else, get unclaimed instance
            {
                try
                {
                    targetInstanceId = (await workersClient.GetUnclaimedInstanceAsync(new Empty())).Id;
                }
                catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
                {
                    _reclaimSemaphore.Release();
                    throw new ApplicationException("No unclaimed instance available");
                }
                catch (Exception e)
                {
                    _reclaimSemaphore.Release();
                    throw new ApplicationException("Failed to fetch unclaimed instance");
                }
            }

            var claim = Ulid.NewUlid();
            var confirmedInstance = await workersClient.ClaimInstanceAsync(new ClaimInstanceMessage
            {
                WorkerUlid = workerState.WorkerUlid.ToString(),
                InstanceId = targetInstanceId,
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
                _reclaimSemaphore.Release();
                throw new ApplicationException($"Failed to re-claim instance {instance.InstanceDetails.Id}");
            }

            instance = workerState.AssignInstance(updatedInstance, newClaim);
        }

        _reclaimSemaphore.Release();
        return instance;
    }

    public async Task<GuildAssignment> GetAssignedGuild(InstanceClaim instance)
    {
        logger.LogTrace("GetAssignedGuild({instance})", instance.InstanceDetails);

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
            throw new ApplicationException($"No guild options set for instance {instance.InstanceDetails.Id}");
        }
        catch (Exception e)
        {
            throw new ApplicationException($"Failed to get guild options for instance {instance.InstanceDetails.Id}");
        }

        // get discord client, create if not exists or settings changed
        var guildAssignment = workerState.GuildAssignment;
        if (guildAssignment is null || !guildAssignment.GuildOptions.HasSameOptions(guildOptions))
        {
            guildAssignment = await workerState.AssignGuild(guildOptions, instance.InstanceDetails.BotToken);
        }

        return guildAssignment;
    }
}