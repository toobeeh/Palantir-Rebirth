using Microsoft.Extensions.Logging;
using Quartz;
using tobeh.Palantir.Core.Quartz.DropScheduler;
using tobeh.Valmar;

namespace tobeh.Palantir.Core.Quartz.BubbleTracer;

public class BubbleTracerJob(ILogger<DropSchedulerJob> logger, Admin.AdminClient adminClient) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        await adminClient.CreateBubbleTracesAsync(new());
    }
}