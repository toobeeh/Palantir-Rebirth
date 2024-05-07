using Quartz;

namespace tobeh.Palantir.Lobbies.Quartz.WorkerLobbyUpdater;

public static class WorkerLobbyUpdaterConfiguration
{
    public static void Configure(IServiceCollectionQuartzConfigurator configurator)
    {
        var jobId = new JobKey($"Worker Lobby Updater");

        configurator.AddJob<WorkerLobbyUpdaterJob>(job => job
            .WithIdentity(jobId));

        configurator.AddTrigger(trigger => trigger
            .ForJob(jobId)
            .StartNow()
            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(20).RepeatForever()));
    }
}