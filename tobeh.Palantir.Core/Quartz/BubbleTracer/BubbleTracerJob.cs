using Microsoft.Extensions.Logging;
using Prometheus;
using Quartz;
using tobeh.Palantir.Core.Quartz.DropScheduler;
using tobeh.Valmar;

namespace tobeh.Palantir.Core.Quartz.BubbleTracer;

public class BubbleTracerJob(ILogger<DropSchedulerJob> logger, Admin.AdminClient adminClient) : IJob
{
    private static readonly Gauge DailyOnlinePlayersGauge = Metrics.CreateGauge(
        "typo_daily_players",
        "The amount of players that have been online in the past 24 hours.");

    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        var response = await adminClient.CreateBubbleTracesAsync(new());
        DailyOnlinePlayersGauge.Set(response.DailyPlayers);
    }
}