using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Quartz;
using tobeh.Valmar;

namespace tobeh.Palantir.Core.Quartz.VolatileDataClearer;

public class VolatileDataClearerJob(ILogger<VolatileDataClearerJob> logger, Admin.AdminClient adminClient) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        await adminClient.ClearVolatileDataAsync(new Empty());
    }
}