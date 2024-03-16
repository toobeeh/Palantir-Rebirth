using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Quartz;
using Valmar;
using Valmar_Client.Grpc;

namespace Palantir_Core.Quartz.OnlineItemsUpdater;

public class OnlineItemsUpdaterJob(ILogger<OnlineItemsUpdaterJob> logger, Admin.AdminClient adminClient, Lobbies.LobbiesClient lobbiesClient) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        var sw = new Stopwatch();
        sw.Start();
        var onlineMembers = await lobbiesClient.GetOnlinePlayers(new Empty()).ToListAsync();
        
        // get sprite, scene, shift choices (add to onlineMembers data) - write them plus rewardee online items
        
        var memberItems = onlineMembers.SelectMany(member =>
        {
            List<OnlineItemMessage> items = new List<OnlineItemMessage>();

            foreach (var lobby in member.JoinedLobbies)
            {
                // add sprites
                items.AddRange(member.SpriteSlots
                    .Select(slot => new OnlineItemMessage
                    {
                        ItemType = OnlineItemType.Sprite,
                        Slot = slot.Slot,
                        ItemId = slot.SpriteId,
                        LobbyKey = lobby.Lobby.Key,
                        LobbyPlayerId = lobby.LobbyPlayerId
                    }));
                
                // add sprite shifts
                items.AddRange(member.SpriteSlots
                    .Where(slot => slot.RainbowShift is not null)
                    .Select(slot => new OnlineItemMessage
                    {
                        ItemType = OnlineItemType.ColorShift,
                        Slot = slot.Slot,
                        ItemId = (int)slot.RainbowShift!,
                        LobbyKey = lobby.Lobby.Key,
                        LobbyPlayerId = lobby.LobbyPlayerId
                    }));
                
                // add scene
                if(member.SceneId is {} sceneValue)
                {
                    items.Add(new OnlineItemMessage
                    {
                        ItemType = OnlineItemType.Scene,
                        Slot = 1,
                        ItemId = sceneValue,
                        LobbyKey = lobby.Lobby.Key,
                        LobbyPlayerId = lobby.LobbyPlayerId
                    });
                }
                
                // add rewardee
                items.Add(new OnlineItemMessage{
                    ItemType = OnlineItemType.Rewardee,
                    Slot = 1,
                    ItemId = 1,
                    LobbyKey = lobby.Lobby.Key,
                    LobbyPlayerId = lobby.LobbyPlayerId
                });
            }

            return items;
        }).ToList();
        
        await adminClient.SetOnlineItemsAsync(new SetOnlineItemsRequest { Items = {memberItems}});
        
        logger.LogInformation("Added {count} items for {memberCount} members after {time}ms", memberItems.Count, onlineMembers.Count, sw.ElapsedMilliseconds);
    }
}