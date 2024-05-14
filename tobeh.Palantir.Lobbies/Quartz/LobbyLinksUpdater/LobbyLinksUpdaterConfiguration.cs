using Quartz;

namespace tobeh.Palantir.Lobbies.Quartz.LobbyLinksUpdater;

public static class LobbyLinksUpdaterConfiguration
{
    public static void Configure(IServiceCollectionQuartzConfigurator configurator)
    {
        var jobId = new JobKey($"Lobby Links Updater");

        configurator.AddJob<LobbyLinksUpdaterJob>(job => job
            .WithIdentity(jobId));

        configurator.AddTrigger(trigger => trigger
            .ForJob(jobId)
            .StartNow()
            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(5).RepeatForever()));
    }
}