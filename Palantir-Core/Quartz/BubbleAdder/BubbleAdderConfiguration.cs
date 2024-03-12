using Quartz;

namespace Palantir_Core.Quartz.BubbleAdder;

public static class BubbleAdderConfiguration
{
    public static void Configure(IServiceCollectionQuartzConfigurator configurator)
    {
        var jobId = new JobKey($"Bubble Adder");

        configurator.AddJob<BubbleAdderJob>(job => job
            .WithIdentity(jobId));

        configurator.AddTrigger(trigger => trigger
            .ForJob(jobId)
            .StartNow()
            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(10).RepeatForever()));
    }
}