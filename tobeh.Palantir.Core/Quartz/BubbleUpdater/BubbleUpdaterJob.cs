using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Quartz;
using tobeh.Palantir.Core.Discord;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Core.Quartz.BubbleUpdater;

public class BubbleUpdaterJob(
    ILogger<BubbleUpdaterJob> logger,
    Drops.DropsClient dropsClient,
    PalantirApiClient discordApiClient,
    Admin.AdminClient adminClient,
    Lobbies.LobbiesClient lobbiesClient) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        var sw = new Stopwatch();
        sw.Start();
        var onlineMembers = await lobbiesClient.GetOnlinePlayers(new Empty()).ToListAsync();
        var memberLogins = onlineMembers.Select(member => member.Login).ToList();
        var dropRate = await dropsClient.GetCurrentBoostFactorAsync(new Empty());

        // set currently playing count
        await discordApiClient.SetStatus(onlineMembers.Select(member => member.Login).Distinct().Count(),
            dropRate.Boost);

        // increment member bubbles
        await adminClient.IncrementMemberBubblesAsync(new IncrementMemberBubblesRequest
            { MemberLogins = { memberLogins } });
        logger.LogInformation("Added Bubbles for {count} members after {time}ms", memberLogins.Count,
            sw.ElapsedMilliseconds);
    }
}