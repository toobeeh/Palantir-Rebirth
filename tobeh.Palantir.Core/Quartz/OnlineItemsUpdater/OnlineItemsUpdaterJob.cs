using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Quartz;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Core.Quartz.OnlineItemsUpdater;

public class OnlineItemsUpdaterJob(
    ILogger<OnlineItemsUpdaterJob> logger,
    Admin.AdminClient adminClient,
    Lobbies.LobbiesClient lobbiesClient) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        var sw = new Stopwatch();
        sw.Start();
        var legacyOnlineMembers = await lobbiesClient.GetOnlinePlayers(new Empty()).ToListAsync();
        var lobbies = await lobbiesClient.GetOnlineLobbyPlayers(new GetOnlinePlayersRequest()).ToListAsync();

        var memberItems = lobbies.SelectMany<SkribblLobbyTypoMembersMessage, OnlineItemMessage>(lobby =>
        {
            var key = GenerateLobbyKey(lobby.LobbyId);

            var sprites = lobby.Members
                .SelectMany(member => member.SpriteSlots.Select(slot =>
                    CreateSpriteItem(key, member.LobbyPlayerId, slot.Slot, slot.SpriteId)));
            var shifts = lobby.Members
                .SelectMany(member => member.SpriteSlots
                    .Where(slot => slot.ColorShift is not null)
                    .Select(slot =>
                        CreateColorShiftItem(key, member.LobbyPlayerId, slot.Slot, (int)slot.ColorShift!)));
            var scenes = lobby.Members
                .Where(member => member.SceneId is not null)
                .Select(member => CreateSceneItem(key, member.LobbyPlayerId, (int)member.SceneId!));
            var sceneThemes = lobby.Members
                .Where(member => member.SceneShift is not null)
                .Select(member =>
                    CreateSceneThemeItem(key, member.LobbyPlayerId, (int)member.SceneId!, (int)member.SceneShift!));
            var rewardees = lobby.Members
                .Select(member => CreateRewardeeItem(key, member.LobbyPlayerId));

            return [..sprites, ..shifts, ..scenes, ..sceneThemes, ..rewardees];
        }).ToList();

        // get sprite, scene, shift choices (add to onlineMembers data) - write them plus rewardee online items
        var legacyMemberItems = legacyOnlineMembers.SelectMany(member =>
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
                    .Where(slot => slot.ColorShift is not null)
                    .Select(slot => new OnlineItemMessage
                    {
                        ItemType = OnlineItemType.ColorShift,
                        Slot = slot.Slot,
                        ItemId = (int)slot.ColorShift!,
                        LobbyKey = lobby.Lobby.Key,
                        LobbyPlayerId = lobby.LobbyPlayerId
                    }));

                // add scene
                if (member.SceneId is { } sceneValue)
                {
                    items.Add(new OnlineItemMessage
                    {
                        ItemType = OnlineItemType.Scene,
                        Slot = 1,
                        ItemId = sceneValue,
                        LobbyKey = lobby.Lobby.Key,
                        LobbyPlayerId = lobby.LobbyPlayerId
                    });

                    // add scene theme
                    if (member.SceneShift is { } sceneShiftValue)
                    {
                        items.Add(new OnlineItemMessage
                        {
                            ItemType = OnlineItemType.SceneTheme,
                            Slot = sceneValue,
                            ItemId = sceneShiftValue,
                            LobbyKey = lobby.Lobby.Key,
                            LobbyPlayerId = lobby.LobbyPlayerId
                        });
                    }
                }

                // add rewardee
                items.Add(new OnlineItemMessage
                {
                    ItemType = OnlineItemType.Rewardee,
                    Slot = 1,
                    ItemId = 1,
                    LobbyKey = lobby.Lobby.Key,
                    LobbyPlayerId = lobby.LobbyPlayerId
                });
            }

            return items;
        }).ToList();

        await adminClient.SetOnlineItemsAsync(new SetOnlineItemsRequest { Items = { legacyMemberItems, memberItems } });

        logger.LogInformation("Added {count} items for {memberCount} members after {time}ms", legacyMemberItems.Count,
            legacyOnlineMembers.Count, sw.ElapsedMilliseconds);
    }

    private static string GenerateLobbyKey(string lobbyId)
    {
        var sum = lobbyId.Sum(c => c);
        var hashed = lobbyId.Select(c => (char)(c + sum)).ToArray();
        return new string(hashed);
    }

    private static OnlineItemMessage CreateRewardeeItem(string key, int lobbyPlayerId)
    {
        return new OnlineItemMessage
        {
            ItemType = OnlineItemType.Rewardee,
            Slot = 1,
            ItemId = 1,
            LobbyKey = key,
            LobbyPlayerId = lobbyPlayerId
        };
    }

    private static OnlineItemMessage CreateSceneItem(string key, int lobbyPlayerId, int sceneId)
    {
        return new OnlineItemMessage
        {
            ItemType = OnlineItemType.Scene,
            Slot = 1,
            ItemId = sceneId,
            LobbyKey = key,
            LobbyPlayerId = lobbyPlayerId
        };
    }

    private static OnlineItemMessage CreateSceneThemeItem(string key, int lobbyPlayerId, int sceneId, int sceneThemeId)
    {
        return new OnlineItemMessage
        {
            ItemType = OnlineItemType.SceneTheme,
            Slot = sceneId,
            ItemId = sceneThemeId,
            LobbyKey = key,
            LobbyPlayerId = lobbyPlayerId
        };
    }

    private static OnlineItemMessage CreateSpriteItem(string key, int lobbyPlayerId, int slot, int spriteId)
    {
        return new OnlineItemMessage
        {
            ItemType = OnlineItemType.Sprite,
            Slot = slot,
            ItemId = spriteId,
            LobbyKey = key,
            LobbyPlayerId = lobbyPlayerId
        };
    }

    private static OnlineItemMessage CreateColorShiftItem(string key, int lobbyPlayerId, int slot, int colorShiftId)
    {
        return new OnlineItemMessage
        {
            ItemType = OnlineItemType.ColorShift,
            Slot = slot,
            ItemId = colorShiftId,
            LobbyKey = key,
            LobbyPlayerId = lobbyPlayerId
        };
    }
}