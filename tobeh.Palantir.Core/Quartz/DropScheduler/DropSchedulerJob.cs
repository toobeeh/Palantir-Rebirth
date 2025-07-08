using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Prometheus;
using Quartz;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Core.Quartz.DropScheduler;

public class DropSchedulerJob(
    ILogger<DropSchedulerJob> logger,
    Events.EventsClient eventsClient,
    Drops.DropsClient dropsClient,
    Lobbies.LobbiesClient lobbiesClient) : IJob
{
    private static readonly Gauge DropDelaySecondsGauge = Metrics.CreateGauge(
        "typo_drop_delay_seconds",
        "The delay in seconds until the next drop is scheduled.");

    private static readonly Gauge DropDelayMinSecondsGauge = Metrics.CreateGauge(
        "typo_drop_delay_min_seconds",
        "The min delay in seconds until the next drop is scheduled.");

    private static readonly Gauge DropDelayMaxSecondsGauge = Metrics.CreateGauge(
        "typo_drop_delay_max_seconds",
        "The max delay in seconds until the next drop is scheduled.");

    private static readonly Counter ScheduledDropTypeCounter = Metrics.CreateCounter(
        "typo_drop_type_counter",
        "The amount of drops scheduled by event drop id.",
        new CounterConfiguration
        {
            LabelNames = new[] { "event_drop_id" }
        });

    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        int delay;
        try
        {
            delay = await ScheduleNextDrop();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to schedule next drop");
            delay = 30; // fallback to 30s for next job trigger if calculations failed (grpc error or something)
        }

        var nextJobDelay = delay + 5; // add 2s between each drop (+ max claim time)
        var newTrigger = TriggerBuilder.Create()
            .StartAt(DateTimeOffset.Now.AddSeconds(nextJobDelay))
            .Build();

        await context.Scheduler.RescheduleJob(context.Trigger.Key, newTrigger);
    }

    private async Task<int> ScheduleNextDrop()
    {
        logger.LogTrace("ScheduleNextDrop()");

        // get current player count
        var lobbies = await lobbiesClient.GetOnlineLobbyPlayers(new GetOnlinePlayersRequest())
            .ToDictionaryAsync(p => p.LobbyId);
        var playerCount = lobbies.Values.SelectMany(lobby => lobby.Members.Select(member => member.Login)).Distinct()
            .Count();

        // get current active boost factor
        var boostFactor = await dropsClient.GetCurrentBoostFactorAsync(new Empty());

        // get random event drop id if event is active
        int? eventDropId = null;
        try
        {
            var currentEvent = await eventsClient.GetCurrentEventAsync(new Empty());

            // for a chance of 50%, show an event drop
            if (new Random().Next(10) >= 5)
            {
                var dropsStream = eventsClient.GetEventDropsOfEvent(new GetEventRequest { Id = currentEvent.Id })
                    .ResponseStream;
                List<EventDropReply> drops = new();

                while (await dropsStream.MoveNext())
                {
                    if (dropsStream.Current.ReleaseEnd.ToDateTimeOffset() > DateTimeOffset.UtcNow
                        && dropsStream.Current.ReleaseStart.ToDateTimeOffset() < DateTimeOffset.UtcNow)
                    {
                        drops.Add(dropsStream.Current);
                    }
                }

                eventDropId = drops[new Random().Next(drops.Count)].Id;
            }
        }
        catch
        {
            // no event active
        }

        // calc timeout for drop
        var bounds = await dropsClient.CalculateDropDelayBoundsAsync(new CalculateDelayRequest
        {
            OnlinePlayerCount = playerCount,
            BoostFactor = boostFactor.Boost
        });
        var randomDelay = new Random().Next(bounds.MinDelaySeconds, bounds.MaxDelaySeconds);

        // schedule drop
        logger.LogInformation(
            "Current drop params: playerCount={playerCount}, boostFactor={boostFactor}, eventDropId={eventDropId}",
            playerCount, boostFactor.Boost, eventDropId);
        logger.LogInformation(
            "Scheduling drop with bounds {bounds.Item1} and {bounds.Item2} => {randomDelay} seconds delay.",
            bounds.MinDelaySeconds, bounds.MaxDelaySeconds, randomDelay);
        await dropsClient.ScheduleDropAsync(new ScheduleDropRequest
            { DelaySeconds = randomDelay, EventDropId = eventDropId });

        DropDelaySecondsGauge.Set(randomDelay);
        DropDelayMinSecondsGauge.Set(bounds.MinDelaySeconds);
        DropDelayMaxSecondsGauge.Set(bounds.MaxDelaySeconds);
        ScheduledDropTypeCounter.WithLabels(eventDropId?.ToString() ?? "regular")
            .Inc();

        return randomDelay;
    }
}