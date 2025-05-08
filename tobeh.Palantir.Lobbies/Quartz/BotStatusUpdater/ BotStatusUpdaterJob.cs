using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using tobeh.Palantir.Commands;
using tobeh.Palantir.Lobbies.Worker;
using tobeh.Valmar;

namespace tobeh.Palantir.Lobbies.Quartz.BotStatusUpdater;

public class BotStatusUpdaterJob(
    Guilds.GuildsClient guildsClient,
    WorkerService workerService,
    ILogger<BotStatusUpdaterJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        var instance = await workerService.ReclaimInstance();
        var guildAssignment = await workerService.GetAssignedGuild(instance);
        var guildOptions = guildAssignment.GuildOptions;

        // update status, if guild has channel set
        if (guildOptions.ChannelId is not null)
        {
            // set bot status
            var guildInfo =
                await guildsClient.GetGuildByIdAsync(new GetGuildByIdMessage { DiscordId = guildOptions.GuildId });
            await guildAssignment.DiscordBotHost.Services.GetRequiredService<DiscordHostedBot>().DiscordClient
                .UpdateStatusAsync(
                    new DiscordActivity(
                        $"{guildInfo.OnlineMemberCount} playing, {guildInfo.ConnectedMemberCount} connected",
                        DiscordActivityType.Custom
                    ));
        }
        else
        {
            logger.LogInformation("No lobbies channel set for server {guildId} in instance {instance}",
                guildOptions.GuildId, instance.InstanceDetails.Id);

            // clear status
            await guildAssignment.DiscordBotHost.Services.GetRequiredService<DiscordClient>().UpdateStatusAsync();
        }
    }
}