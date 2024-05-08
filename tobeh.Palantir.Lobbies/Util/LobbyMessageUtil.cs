using DSharpPlus;
using DSharpPlus.Entities;
using tobeh.Valmar;

namespace tobeh.Palantir.Lobbies.Util;

public record LobbyMessageSplit(DiscordMessage? Message, string Content);

public class LobbyMessageUtil
{
    private static readonly string[] LobbyEmojis =
    [
        "💎", "✨", "🐈", "🌄", "⛈️", "🌈", "🍔", "🍟", "😂", "❤️", "😍", "🤣", "🙏", "😊", "🚀", "🎉", "💕", "🔥", "😁",
        "👍", "🎂", "📷", "👀", "🌸", "💪", "🌟", "🎁", "🐶", "👏", "🎄", "💖", "✈️", "🍰", "👌", "🥰", "🏖️", "🍕",
        "👻", "🌼", "🌍", "🐱", "🥳", "📚", "🌧️", "⏰", "🍓", "🏀", "👠", "🍦", "🍾", "🏡", "🍂", "🔒", "👼", "🧡", "⚽",
        "🎵", "🎮"
    ];

    public static async Task<List<DiscordMessage>> GetMessageCandidatesInChannel(DiscordChannel channel, ulong authorId,
        ulong? startMessage = null)
    {
        const int limit = 10;
        var candidates = new List<DiscordMessage>();
        var messages = startMessage is { } id ? channel.GetMessagesBeforeAsync(id) : channel.GetMessagesAsync(limit);

        await foreach (var message in messages)
        {
            if (message.Author?.Id == authorId)
            {
                candidates.Add(message);
            }
            else break;
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
        messages.AddRange(availableMessages.Skip(messages.Count).Select(msg => new LobbyMessageSplit(msg, "_ _")));

        return messages;
    }

    public static string BuildHeader(int serverInvite)
    {
        return $"\n_ _```ansi\n{Formatter.Colorize("Who's around on skribbl?", AnsiColor.Blue)}\n```\n" +
               $"Refreshed: {Formatter.Timestamp(DateTimeOffset.UtcNow)}\nClick to connect: https://www.typo.rip/invite/{serverInvite}";
    }

    public static List<string> BuildLobbies(List<LobbyReply> lobbies, List<MemberReply> memberDetails, long guildId,
        int serverInvite)
    {
        var memberDict = memberDetails.Where(m => m.ServerConnections.Contains(serverInvite))
            .ToDictionary(member => member.Login);

        return lobbies.OrderBy(lobby => lobby.PalantirDetails.Id).Select((lobby, index) =>
        {
            var playerDict = lobby.SkribblDetails.Players.ToDictionary(player => player.LobbyPlayerId);
            var ranks = lobby.SkribblDetails.Players
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
                    Id = p.LobbyPlayerId
                })
                .ToDictionary(p => p.Id, p => p.Rank);
            var members = lobby.Players
                .Where(p => p.LobbyPlayerId is not null && memberDict.ContainsKey(p.Login))
                .ToDictionary(p => p.LobbyPlayerId!.Value, p => memberDict[p.Login]);

            var link = lobby.SkribblDetails.Private
                ? lobby.PalantirDetails.Restriction switch
                {
                    "#nojoin" => "`Closed private game`",
                    "restricted" => "`Restricted private game`",
                    "unrestricted" => $"[Join Lobby](<{lobby.SkribblDetails.Link}>)",
                    _ => lobby.PalantirDetails.Restriction == guildId.ToString()
                        ? $"[Join Lobby](<{lobby.SkribblDetails.Link}>)"
                        : "`Server restricted private game`"
                }
                : $"[Join Lobby](<{lobby.SkribblDetails.Link}>)";

            var lobbyEmote = LobbyEmojis[Convert.ToInt64(lobby.PalantirDetails.Id) % LobbyEmojis.Length];

            var palantirPlayers = lobby.Players
                .Where(p => memberDict.ContainsKey(p.Login))
                .Select(player => player.LobbyPlayerId is not { } id
                    ? ""
                    : $"{playerDict[id].Name,-20} {playerDict[id].Score + " pts " + ranks[id],-15} {
                        (string.IsNullOrWhiteSpace(members[id].PatronEmoji) ? "🔮 " + members[id].Bubbles : members[id].PatronEmoji)
                    } {(playerDict[id].Drawing ? "🖌️" : "")}")
                .ToList();

            if (palantirPlayers.Count == 0) return "";

            var skribblPlayers = lobby.SkribblDetails.Players
                .Where(p => !members.ContainsKey(p.LobbyPlayerId))
                .Select(p => $"{
                    (string.IsNullOrWhiteSpace(ranks[p.LobbyPlayerId]) ? "" : $"`{ranks[p.LobbyPlayerId]}`")
                } {Formatter.Sanitize(p.Name)} {(p.Drawing ? "`🖌️`" : "")}").ToList();

            return
                $">   **#{index + 1}**  `{lobbyEmote}`     {lobby.SkribblDetails.Language}     **|**     Round {lobby.SkribblDetails.Round}     **|**     " +
                $"{(lobby.SkribblDetails.Private ? "Private" : "Public")}     **|**     {lobby.SkribblDetails.Players.Count} Players\n" +
                (!string.IsNullOrWhiteSpace(lobby.PalantirDetails.Description)
                    ? $"> `{lobby.PalantirDetails.Description}`\n"
                    : "") +
                $"> {link}\n" +
                (palantirPlayers.Count > 0 ? $"```fix\n{string.Join("\n", palantirPlayers)}```" : "") +
                $" {string.Join(", ", skribblPlayers)} ";
        }).ToList();
    }
}