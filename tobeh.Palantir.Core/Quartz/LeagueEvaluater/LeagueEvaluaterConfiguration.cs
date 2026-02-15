using Quartz;
using tobeh.Palantir.Core.Quartz.BubbleTracer;

namespace tobeh.Palantir.Core.Quartz.LeagueEvaluater;

public static class LeagueEvaluaterConfiguration
{
    public static void Configure(IServiceCollectionQuartzConfigurator configurator)
    {
        var jobId = new JobKey($"League Evaluater");

        configurator.AddJob<BubbleTracerJob>(job => job
            .WithIdentity(jobId));

        configurator.AddTrigger(trigger => trigger
            .ForJob(jobId)
            .StartNow()
            .WithSchedule(
                CronScheduleBuilder.MonthlyOnDayAndHourAndMinute(1, 0, 1).InTimeZone(TimeZoneInfo.Utc)
            ));
    }
}