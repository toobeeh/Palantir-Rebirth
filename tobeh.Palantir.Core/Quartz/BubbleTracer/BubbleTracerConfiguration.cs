using Quartz;

namespace tobeh.Palantir.Core.Quartz.BubbleTracer;

public static class BubbleTracerConfiguration
{
    public static void Configure(IServiceCollectionQuartzConfigurator configurator)
    {
        var jobId = new JobKey($"Bubble Tracer");

        configurator.AddJob<BubbleTracerJob>(job => job
            .WithIdentity(jobId));

        configurator.AddTrigger(trigger => trigger
            .ForJob(jobId)
            .StartNow()
            .WithDailyTimeIntervalSchedule(schedule => schedule
                .WithIntervalInHours(24)
                .StartingDailyAt(TimeOfDay.HourAndMinuteOfDay(23, 59))
                .InTimeZone(TimeZoneInfo.Utc)));
    }
}