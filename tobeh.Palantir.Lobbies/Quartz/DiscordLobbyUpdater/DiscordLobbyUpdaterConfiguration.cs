using Quartz;

namespace tobeh.Palantir.Lobbies.Quartz.DiscordLobbyUpdater;

public static class DiscordLobbyUpdaterConfiguration
{
    public static void Configure(IServiceCollectionQuartzConfigurator configurator)
    {
        var jobId = new JobKey($"Discord Lobby Updater");

        configurator.AddJob<DiscordLobbyUpdaterJob>(job => job
            .WithIdentity(jobId));

        configurator.AddTrigger(trigger => trigger
            .ForJob(jobId)
            .StartNow()
            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(20).RepeatForever()));
    }
}