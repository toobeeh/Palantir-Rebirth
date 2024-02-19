using Quartz;

namespace Palantir_Core.Quartz.PatronUpdater;

public static class PatronUpdaterConfiguration
{
    public static void Configure(IServiceCollectionQuartzConfigurator configurator)
    {
        var jobId = new JobKey($"Patron Updater");

        configurator.AddJob<PatronUpdaterJob>(job => job
            .WithIdentity(jobId));

        configurator.AddTrigger(trigger => trigger
            .ForJob(jobId)
            .StartNow()
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));
    }
}