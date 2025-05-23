using DSharpPlus;
using DSharpPlus.Entities;
using tobeh.Valmar;

namespace tobeh.Palantir.Lobbies.Util;

public record LobbyMessageSplit(DiscordMessage? Message, string Content);

public class LobbyMessageUtil
{
    private static readonly string[] LobbyEmojis =
    [
        "<a:l1:1238936190313566280>",
        "<a:l10:1238936193329270865>",
        "<a:l11:1238936196436983828>",
        "<a:l12:1238936200258261124>",
        "<a:l13:1238936202468393022>",
        "<a:l14:1238936206168035338>",
        "<a:l22:1238936215181332593>",
        "<a:l21:1238936218369261618>",
        "<a:l20:1238936220684390543>",
        "<a:l2:1238936224207470692>",
        "<a:l19:1238936226955006003>",
        "<a:l18:1238936229186375761>",
        "<a:l17:1238936231618936862>",
        "<a:l16:1238936234525720648>",
        "<a:l15:1238936208961179819>",
        "<a:l23:1238936212094586920>",
        "<a:l24:1238936237134446722>",
        "<a:l25:1238936240204812370>",
        "<a:l26:1238936243195220079>",
        "<a:l27:1238936246328361104>",
        "<a:l28:1238936249637666977>",
        "<a:l29:1238936252041138318>",
        "<a:l3:1238936255400644660>",
        "<a:l30:1238936257996783707>",
        "<a:l43:1238936289802326037>",
        "<a:l42:1238936293212426420>",
        "<a:l41:1238936295938592923>",
        "<a:l40:1238936299956604979>",
        "<a:l4:1238936271863414786>",
        "<a:l36:1238936275302748230>",
        "<a:l33:1238936278653866015>",
        "<a:l32:1238936281556320278>",
        "<a:l31:1238936261050499172>",
        "<a:l5:1238936269027803207>",
        "<a:l6:1238936266490511523>",
        "<a:l7:1238936263571148942>",
        "<a:l8:1238936284035285114>",
        "<a:l9:1238936287537401930>"
    ];

    public static async Task<List<DiscordMessage>> GetMessageCandidatesInChannel(DiscordChannel channel, ulong authorId,
        ulong? startMessage = null)
    {
        const int limit = 10;
        var candidates = new List<DiscordMessage>();
        var messages = startMessage is { } id ? channel.GetMessagesBeforeAsync(id) : channel.GetMessagesAsync(limit);

        await foreach (var message in messages)
        {
            if (message.Author?.Id == authorId) candidates.Insert(0, message);
        }

        if (candidates.Count == limit)
        {
            candidates.AddRange(await GetMessageCandidatesInChannel(channel, authorId, candidates.Last().Id));
        }

        return candidates;
    }

    public static List<LobbyMessageSplit> SplitContentToMessages(List<DiscordMessage> availableMessages, string header,
        List<string> lobbies)
    {
        const int limit = 2000;

        if (lobbies.Count == 0)
            lobbies.Add("No one is playing right now :(\nJoin a lobby on skribbl to show your friends you're around!");

        var splits = new List<string> { header };
        while (lobbies.Count > 0)
        {
            var lastSplit = splits.Last();
            var nextLobby = lobbies.First();
            lobbies.RemoveAt(0);
            if (string.IsNullOrWhiteSpace(nextLobby)) continue;

            if (lastSplit.Length + nextLobby.Length + 50 < limit) splits[^1] = $"{lastSplit}\n_ _\n_ _\n{nextLobby}";
            else
            {
                splits.Add($"\n_ _\n_ _\n{nextLobby}");
            }
        }

        var messages = splits
            .Select((split, index) => new LobbyMessageSplit(availableMessages.ElementAtOrDefault(index), split))
            .ToList();
        messages.InsertRange(0,
            availableMessages.Skip(messages.Count).Select(msg => new LobbyMessageSplit(msg, "_ _")));

        return messages;
    }

    public static string BuildHeader(int? serverInvite, EventReply? activeEvent)
    {
        var eventInfo = activeEvent is null
            ? ""
            : $"\n_ _```ansi\n{Formatter.Colorize($"{activeEvent.Name} Event", AnsiColor.Magenta)}\n```\n" +
              $"> Ends {Formatter.Timestamp(activeEvent.EndDate.ToDateTimeOffset())}   **|**   `/event view {activeEvent.Id}`\n" +
              $"> `{activeEvent.Description}`\n_ _";

        return $"{eventInfo}\n_ _```ansi\n{Formatter.Colorize("Who's around on skribbl?", AnsiColor.Blue)}\n```\n" +
               $"Refreshed: {Formatter.Timestamp(DateTimeOffset.UtcNow)}" +
               (serverInvite is null ? "" : $"\nClick to connect: https://www.typo.rip/invite/{serverInvite}");
    }

    public static List<string> BuildLobbies(List<SkribblLobbyMessage> lobbies,
        List<SkribblLobbyTypoMembersMessage> lobbyMembers, Dictionary<string, string> proxyLinks, long guildId)
    {
        var lobbyData = lobbies.ToDictionary(lobby => lobby.SkribblState.LobbyId, lobby =>
        {
            return new
            {
                LobbyState = lobby,
                Members = lobbyMembers.FirstOrDefault(m => m.LobbyId == lobby.SkribblState.LobbyId)?.Members,
                LobbyLink = proxyLinks.TryGetValue(lobby.SkribblState.LobbyId, out var link)
                    ? link
                    : $"https://skribbl.io?{lobby.SkribblState.LobbyId}"
            };
        });

        return lobbyData
            .Values
            .Where(lobby => lobby.Members?.Count > 0)
            .OrderBy(lobby => lobby.LobbyState.TypoSettings.FirstSeen.Seconds)
            .Select((lobby, index) =>
            {
                if (lobby.Members is null) throw new NullReferenceException("Invalid state in lobby without members");

                var playerDict = lobby.LobbyState.SkribblState.Players.ToDictionary(player => player.PlayerId);
                var ranks = lobby.LobbyState.SkribblState.Players
                    .OrderByDescending(p => p.Score)
                    .Select((p, idx) => new
                    {
                        Rank = idx switch
                        {
                            0 => "🏆",
                            1 => "🥈",
                            2 => "🥉",
                            _ => ""
                        },
                        Id = p.PlayerId
                    })
                    .ToDictionary(p => p.Id, p => p.Rank);

                string? lobbyClosedReason = null;
                if (lobby.LobbyState.TypoSettings.Description.StartsWith("#nojoin"))
                {
                    lobbyClosedReason =
                        "Closed Game";
                }
                else if (lobby.LobbyState.TypoSettings.WhitelistAllowedServers)
                {
                    if (lobby.LobbyState.TypoSettings.AllowedServers.Count == 0)
                        lobbyClosedReason = "Lobby joining disabled";
                    else if (!lobby.LobbyState.TypoSettings.AllowedServers.Contains(guildId))
                        lobbyClosedReason = "Lobby joining restricted";
                }

                var lobbyEmote =
                    LobbyEmojis[Convert.ToInt64(lobby.LobbyState.TypoSettings.FirstSeen.Seconds) % LobbyEmojis.Length];

                var palantirPlayers = lobby.Members
                    .OrderBy(player => player.LobbyPlayerId)
                    .Select(player =>
                        $"{playerDict[player.LobbyPlayerId].Name,-20} {playerDict[player.LobbyPlayerId].Score + " pts " + ranks[player.LobbyPlayerId],-15} {
                            (string.IsNullOrWhiteSpace(player.PatronEmoji) ? "🔮 " + player.Bubbles : player.PatronEmoji)
                        } {(lobby.LobbyState.SkribblState.DrawerId == player.LobbyPlayerId ? "🖌️" : "")}")
                    .Where(player => !string.IsNullOrWhiteSpace(player))
                    .ToList();

                var skribblPlayers = lobby.LobbyState.SkribblState.Players
                    .Where(p => lobby.Members.All(member => member.LobbyPlayerId != p.PlayerId))
                    .Select(p => $"{
                        (string.IsNullOrWhiteSpace(ranks[p.PlayerId]) ? "" : $"`{ranks[p.PlayerId]}`")
                    } {Formatter.Sanitize(p.Name)} {(lobby.LobbyState.SkribblState.DrawerId == p.PlayerId ? "`🖌️`" : "")}")
                    .ToList();

                var description = string.IsNullOrWhiteSpace(lobby.LobbyState.TypoSettings.Description)
                    ? ""
                    : lobby.LobbyState.TypoSettings.Description;
                description = Formatter.Sanitize(description.Substring(0, Math.Min(150, description.Length)))
                    .Replace("`", "")
                    .Replace("\n", " ~ ");

                return
                    $">   **#{index + 1}**  {lobbyEmote}     {Formatter.Sanitize(lobby.LobbyState.SkribblState.Settings.Language)}     **|**     Round {lobby.LobbyState.SkribblState.Round} / {lobby.LobbyState.SkribblState.Settings.Rounds}     **|**     " +
                    $"{(lobby.LobbyState.SkribblState.OwnerId is not null ? "Custom" : "Public")}     **|**     {lobby.LobbyState.SkribblState.Players.Count} / {lobby.LobbyState.SkribblState.Settings.Players} Players     **|**     Since {Formatter.Timestamp(lobby.LobbyState.TypoSettings.FirstSeen.ToDateTimeOffset())}\n" +
                    (description.Length > 0 ? $"> `{description}` \n" : "") +
                    $"> {(lobbyClosedReason is null ? $"[Join Lobby](<{lobby.LobbyLink}>)" : $"`{lobbyClosedReason}`")}\n" +
                    (palantirPlayers.Count > 0 ? $"```fix\n{string.Join("\n", palantirPlayers)}```" : "") +
                    $" {string.Join(", ", skribblPlayers)} ";
            }).ToList();
    }
}