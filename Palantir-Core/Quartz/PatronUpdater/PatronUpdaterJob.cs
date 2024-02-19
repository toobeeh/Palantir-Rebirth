using Microsoft.Extensions.Logging;
using Palantir_Core.Patreon;
using Quartz;

namespace Palantir_Core.Quartz.PatronUpdater;

public class PatronUpdaterJob(ILogger<PatronUpdaterJob> logger, PatreonApiClient client) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);
        
        var subs = await client.GetCurrentSubscriptions();
        
        logger.LogDebug("Found {patronCount} patrons and {patronizerCount} patronizers", subs.Patrons.Count, subs.Patronizer.Count);
    }
}