using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Palantir_Core.Patreon;
using Quartz;
using Valmar;

namespace Palantir_Core.Quartz.PatronUpdater;

public class PatronUpdaterJob(ILogger<PatronUpdaterJob> logger, PatreonApiClient client, Members.MembersClient service) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);
        
        var subs = await client.GetCurrentSubscriptions();
        
        logger.LogDebug("Found {patronCount} patrons and {patronizerCount} patronizers", subs.Patrons.Count, subs.Patronizer.Count);
        
        foreach (var id in subs.Patronizer)
        {
            var member = await service.GetMemberByDiscordIdAsync(new() {Id = id});
            logger.LogDebug(member.Username + " is a patronizer");
        }
    }
}