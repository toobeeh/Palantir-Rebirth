using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using tobeh.Palantir.Commands;
using tobeh.Palantir.Lobbies.Util;
using tobeh.Palantir.Lobbies.Worker;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Lobbies.Quartz.DiscordLobbyUpdater;

public class DiscordLobbyUpdaterJob(
    Valmar.Lobbies.LobbiesClient lobbiesClient,
    Members.MembersClient membersClient,
    Events.EventsClient eventsClient,
    WorkerService workerService,
    ILogger<DiscordLobbyUpdaterJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        var instance = await workerService.ReclaimInstance();
        var guildAssignment = await workerService.GetAssignedGuild(instance);
        var guildOptions = guildAssignment.GuildOptions;
        var client = guildAssignment.DiscordBotHost.Services.GetRequiredService<DiscordHostedBot>().DiscordClient;

        // build lobbies, if enabled
        if (guildOptions.ChannelId is not null)
        {
            var channel = await client.GetChannelAsync((ulong)guildOptions.ChannelId);
            var lobbies = await lobbiesClient.GetCurrentLobbies(new Empty()).ToListAsync();
            var memberLogins = lobbies.SelectMany(lobby => lobby.Players.Select(player => player.Login));
            var memberDetails = await
                membersClient.GetMembersByLogin(new GetMembersByLoginMessage { Logins = { memberLogins } })
                    .ToListAsync();

            // repalce links if proxy enabled
            if (guildOptions.ProxyLinks)
            {
                foreach (var lobby in lobbies)
                {
                    var encryptedLink = await lobbiesClient.EncryptLobbyLinkTokenAsync(new PlainLobbyLinkMessage
                    {
                        Link = lobby.SkribblDetails.Link,
                        GuildId = guildAssignment.GuildOptions.GuildId
                    });
                    lobby.SkribblDetails.Link =
                        $"https://www.typo.rip/join?token={Uri.EscapeDataString(encryptedLink.Token)}";
                }
            }

            EventReply? activeEvent = null;
            try
            {
                activeEvent = await eventsClient.GetCurrentEventAsync(new Empty());
            }
            catch
            {
                /* when no event active */
            }

            var header =
                LobbyMessageUtil.BuildHeader(guildOptions.ShowInvite ? guildOptions.Invite : null, activeEvent);
            var lobbiesContent =
                LobbyMessageUtil.BuildLobbies(lobbies, memberDetails, guildOptions.GuildId, guildOptions.Invite);
            var availableMessages = await LobbyMessageUtil.GetMessageCandidatesInChannel(
                channel, client.CurrentUser.Id);
            var splits = LobbyMessageUtil.SplitContentToMessages(availableMessages, header, lobbiesContent);

            await SendMessageSplits(splits, channel);

            logger.LogInformation("Updated discord lobbies for server {guildId} in instance {instance}",
                guildOptions.GuildId, instance.InstanceDetails.Id);
        }
        else
        {
            logger.LogInformation("No lobbies channel set for server {guildId} in instance {instance}",
                guildOptions.GuildId, instance.InstanceDetails.Id);
        }
    }

    private async Task SendMessageSplits(List<LobbyMessageSplit> splits, DiscordChannel channel)
    {
        foreach (var split in splits)
        {
            if (split.Message is null)
            {
                await channel.SendMessageAsync(split.Content);
            }
            else
            {
                try
                {
                    if (split.Message.Content != split.Content) await split.Message.ModifyAsync(split.Content);
                }
                catch (RateLimitException e)
                {
                    var retryIn = e.Response?.Headers.RetryAfter?.Delta;
                    logger.LogWarning("Skipping message update because {retryIn}s ahead of rate limit", retryIn);
                }
            }
        }
    }
}