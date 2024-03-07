using System.Collections.Concurrent;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Palantir_Core.Discord;
using Palantir_Core.Patreon;
using Quartz;
using Valmar;

namespace Palantir_Core.Quartz.DropScheduler;

public class DropSchedulerJob(ILogger<DropSchedulerJob> logger, Events.EventsClient eventsClient, Drops.DropsClient dropsClient, Lobbies.LobbiesClient lobbiesClient) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        // get current player count
        var playerCount = 0;
        var onlinePlayersStream = lobbiesClient.GetOnlinePlayers(new Empty()).ResponseStream;
        while(await onlinePlayersStream.MoveNext())
        {
            playerCount++;
        }
        
        // get current active boost factor
        var boostFactor = await dropsClient.GetCurrentBoostFactorAsync(new Empty());

        // get random event drop id if event is active
        int? eventDropId = null;
        try
        {
            var currentEvent = await eventsClient.GetCurrentEventAsync(new Empty());

            // for a chance of 50%, show an event drop
            if (new Random().Next(10) > 5)
            {
                var dropsStream = eventsClient.GetEventDropsOfEvent(new GetEventRequest { Id = currentEvent.Id })
                    .ResponseStream;
                List<EventDropReply> drops = new();

                while (await dropsStream.MoveNext())
                {
                    drops.Add(dropsStream.Current);
                }
                
                eventDropId = drops[new Random().Next(drops.Count)].Id;
            }
        }
        catch
        {
            // no event active
        }
        
        // calc timeout for dro
        var bounds = await dropsClient.CalculateDropDelayBoundsAsync(new CalculateDelayRequest
        {
            OnlinePlayerCount = playerCount,
            BoostFactor = boostFactor.Boost
        });
        var randomDelay = new Random().Next(bounds.MinDelaySeconds, bounds.MaxDelaySeconds);        
        
        // schedule drop
        logger.LogInformation("Current drop params: playerCount={playerCount}, boostFactor={boostFactor}, eventDropId={eventDropId}", playerCount, boostFactor.Boost, eventDropId);
        logger.LogInformation("Scheduling drop with bounds {bounds.Item1} and {bounds.Item2} => {randomDelay} seconds delay.", bounds.MinDelaySeconds, bounds.MaxDelaySeconds, randomDelay);
        // await dropsClient.ScheduleDropAsync(new ScheduleDropRequest
        //    { DelaySeconds = randomDelay, EventDropId = eventDropId });

        var nextJobDelay = randomDelay + 5; // add 2s between each drop (max claim time)
        var newTrigger = TriggerBuilder.Create()
            .StartAt(DateTimeOffset.Now.AddSeconds(nextJobDelay))
            .Build();

        await context.Scheduler.RescheduleJob(context.Trigger.Key, newTrigger);
    }
}