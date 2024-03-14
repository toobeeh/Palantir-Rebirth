using Quartz;

namespace Palantir_Core.Quartz.BubbleUpdater;

public static class BubbleUpdaterConfiguration
{
    public static void Configure(IServiceCollectionQuartzConfigurator configurator)
    {
        var jobId = new JobKey($"Bubble Updater");

        configurator.AddJob<BubbleUpdaterJob>(job => job
            .WithIdentity(jobId));

        configurator.AddTrigger(trigger => trigger
            .ForJob(jobId)
            .StartNow()
            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(10).RepeatForever()));
    }
}