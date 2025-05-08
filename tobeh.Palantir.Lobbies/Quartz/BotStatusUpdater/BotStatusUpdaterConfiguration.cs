using Quartz;

namespace tobeh.Palantir.Lobbies.Quartz.BotStatusUpdater;

public static class BotStatusUpdaterConfiguration
{
    public static void Configure(IServiceCollectionQuartzConfigurator configurator)
    {
        var jobId = new JobKey($"Bot Status Updater");

        configurator.AddJob<BotStatusUpdaterJob>(job => job
            .WithIdentity(jobId));

        configurator.AddTrigger(trigger => trigger
            .ForJob(jobId)
            .StartNow()
            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(5).RepeatForever()));
    }
}