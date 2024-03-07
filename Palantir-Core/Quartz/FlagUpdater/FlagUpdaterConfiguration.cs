using Quartz;

namespace Palantir_Core.Quartz.FlagUpdater;

public static class FlagUpdaterConfiguration
{
    public static void Configure(IServiceCollectionQuartzConfigurator configurator)
    {
        var jobId = new JobKey($"Flag Updater");

        configurator.AddJob<FlagUpdaterJob>(job => job
            .WithIdentity(jobId));

        configurator.AddTrigger(trigger => trigger
            .ForJob(jobId)
            .StartNow()
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(2).RepeatForever()));
    }
}