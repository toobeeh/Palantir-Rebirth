using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Prometheus;
using Quartz;
using tobeh.Palantir.Core.Discord;
using tobeh.Palantir.Core.Quartz.RoleUpdater;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Core.Quartz.BubbleUpdater;

public class BubbleUpdaterJob(
    ILogger<BubbleUpdaterJob> logger,
    Drops.DropsClient dropsClient,
    PalantirApiClient discordApiClient,
    Admin.AdminClient adminClient,
    MemberRoleUpdateCollector roleUpdateCollector,
    Lobbies.LobbiesClient lobbiesClient) : IJob
{
    private static readonly Gauge OnlineMemberCountGauge = Metrics.CreateGauge(
        "typo_online_members",
        "The amount of currently online members.");

    private static readonly Gauge BoostFactorGauge = Metrics.CreateGauge(
        "typo_boost_factor",
        "The currently active boost factor.");

    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        var sw = new Stopwatch();
        sw.Start();
        var dropRate = await dropsClient.GetCurrentBoostFactorAsync(new Empty());

        var lobbies = await lobbiesClient.GetOnlineLobbyPlayers(new GetOnlinePlayersRequest()).ToListAsync();
        var lobbyLogins = lobbies.SelectMany(lobby => lobby.Members.Select(member => member.Login)).Distinct().ToList();

        // set currently playing count
        await discordApiClient.SetStatus(lobbyLogins.Count,
            dropRate.Boost);

        // set logins to update in role updater
        lobbyLogins.ForEach(roleUpdateCollector.MarkLoginForUpdate);

        // increment member bubbles
        await adminClient.IncrementMemberBubblesAsync(new IncrementMemberBubblesRequest
            { MemberLogins = { lobbyLogins } });
        logger.LogInformation("Added Bubbles for {count} members after {time}ms", lobbyLogins.Count,
            sw.ElapsedMilliseconds);

        // set online member count gauge
        OnlineMemberCountGauge.Set(lobbyLogins.Count);

        // set boost factor gauge
        BoostFactorGauge.Set(dropRate.Boost);
    }
}