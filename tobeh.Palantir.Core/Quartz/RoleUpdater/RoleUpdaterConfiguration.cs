using Quartz;

namespace tobeh.Palantir.Core.Quartz.RoleUpdater;

public static class RoleUpdaterConfiguration
{
    public static void Configure(IServiceCollectionQuartzConfigurator configurator)
    {
        var jobId = new JobKey($"Role Updater");

        configurator.AddJob<RoleUpdaterJob>(job => job
            .WithIdentity(jobId));

        configurator.AddTrigger(trigger => trigger
            .ForJob(jobId)
            .StartNow()
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(2).RepeatForever()));
    }
}