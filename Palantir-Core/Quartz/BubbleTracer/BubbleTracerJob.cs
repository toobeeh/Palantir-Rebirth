using Microsoft.Extensions.Logging;
using Palantir_Core.Quartz.DropScheduler;
using Quartz;
using tobeh.Valmar;

namespace Palantir_Core.Quartz.BubbleTracer;

public class BubbleTracerJob(ILogger<DropSchedulerJob> logger, Admin.AdminClient adminClient) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        await adminClient.CreateBubbleTracesAsync(new());
    }
}