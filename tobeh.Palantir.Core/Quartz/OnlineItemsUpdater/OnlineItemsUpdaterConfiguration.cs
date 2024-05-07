using Quartz;

namespace tobeh.Palantir.Core.Quartz.OnlineItemsUpdater;

public static class OnlineItemsUpdaterConfiguration
{
    public static void Configure(IServiceCollectionQuartzConfigurator configurator)
    {
        var jobId = new JobKey($"Online Items Updater");

        configurator.AddJob<OnlineItemsUpdaterJob>(job => job
            .WithIdentity(jobId));

        configurator.AddTrigger(trigger => trigger
            .ForJob(jobId)
            .StartNow()
            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(5).RepeatForever()));
    }
}