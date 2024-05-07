using Quartz;

namespace tobeh.Palantir.Core.Quartz.DropScheduler;

public static class DropSchedulerConfiguration
{
    public static void Configure(IServiceCollectionQuartzConfigurator configurator)
    {
        var jobId = new JobKey($"Drop Scheduler");

        configurator.AddJob<DropSchedulerJob>(job => job
            .WithIdentity(jobId));

        configurator.AddTrigger(trigger => trigger
            .ForJob(jobId)
            .StartNow()
            .WithSimpleSchedule(schedule =>
                schedule.WithIntervalInSeconds(5))); // run once after 5s, then reschedule in execute
    }
}