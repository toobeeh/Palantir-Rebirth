using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Palantir_Core.Grpc;
using Quartz;
using Valmar;

namespace Palantir_Core.Quartz.BubbleUpdater;

public class BubbleUpdaterJob(ILogger<BubbleUpdaterJob> logger, Admin.AdminClient adminClient, Lobbies.LobbiesClient lobbiesClient) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        var sw = new Stopwatch();
        sw.Start();
        var onlineMembers = await lobbiesClient.GetOnlinePlayers(new Empty()).ToListAsync();
        var memberLogins = onlineMembers.Select(member => member.Login).ToList();
        
        await adminClient.IncrementMemberBubblesAsync(new IncrementMemberBubblesRequest { MemberLogins = { memberLogins } });
        logger.LogInformation("Added Bubbles for {count} members after {time}ms", memberLogins.Count, sw.ElapsedMilliseconds);
    }
}