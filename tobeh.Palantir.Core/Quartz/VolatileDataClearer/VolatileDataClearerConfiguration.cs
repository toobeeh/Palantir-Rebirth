using Quartz;

namespace tobeh.Palantir.Core.Quartz.VolatileDataClearer;

public static class VolatileDataClearerConfiguration
{
    public static void Configure(IServiceCollectionQuartzConfigurator configurator)
    {
        var jobId = new JobKey($"Volatile Data Clearer");

        configurator.AddJob<VolatileDataClearerJob>(job => job
            .WithIdentity(jobId));

        configurator.AddTrigger(trigger => trigger
            .ForJob(jobId)
            .StartNow()
            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(5).RepeatForever()));
    }
}