using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Palantir_Commands.Discord.Checks;
using Palantir_Commands.Services;
using tobeh.TypoImageGen;
using tobeh.TypoImageGen.Client.Util;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace Palantir_Commands.Discord.Commands;

public class PatronCommands(
    ILogger<PatronCommands> logger,
    MemberContext memberContext,
    Inventory.InventoryClient inventoryClient,
    Card.CardClient cardClient,
    ImageGenerator.ImageGeneratorClient imageGeneratorClient,
    Stats.StatsClient statsClient
)
{
    [Command("card"), RequirePalantirMember(MemberFlagMessage.Patron),
     SlashCommandTypes(DiscordApplicationCommandType.SlashCommand, DiscordApplicationCommandType.MessageContextMenu)]
    public async Task ShowUserCard(CommandContext context)
    {
        logger.LogTrace("ShowUserCard()");

        var member = memberContext.Member;
        var spriteInv = await inventoryClient.GetSpriteInventory(new GetSpriteInventoryRequest { Login = member.Login })
            .ToListAsync();
        var firstSeen =
            await inventoryClient.GetFirstSeenDateAsync(new GetFirstSeenDateRequest { Login = member.Login });
        var dropCredit = await inventoryClient.GetDropCreditAsync(new GetDropCreditRequest { Login = member.Login });
        var colorMaps = spriteInv
            .Where(spt => spt.ColorShift != null && spt.Slot > 0)
            .Select(slot => new ColorMapMessage { HueShift = slot.ColorShift ?? 100, SpriteId = slot.SpriteId });
        var dropLeaderboard =
            await statsClient.GetLeaderboardAsync(new GetLeaderboardMessage { Mode = LeaderboardMode.Drops });
        var bubbleLeaderboard =
            await statsClient.GetLeaderboardAsync(new GetLeaderboardMessage { Mode = LeaderboardMode.Bubbles });
        var dropRank = dropLeaderboard.Entries.ToList().FirstOrDefault(entry => entry.Login == member.Login)?.Rank ?? 0;
        var bubbleRank =
            bubbleLeaderboard.Entries.ToList().FirstOrDefault(entry => entry.Login == member.Login)?.Rank ?? 0;

        var combo = spriteInv.Where(slot => slot.Slot > 0).OrderBy(slot => slot.Slot).Select(slot => slot.SpriteId);

        var card = await imageGeneratorClient.GenerateCard(new GenerateCardMessage
        {
            SettingsOwnerLogin = member.Login,
            Username = context.User.Username,
            IsModerator = member.MappedFlags.Contains(MemberFlagMessage.Moderator),
            IsPatron = member.MappedFlags.Contains(MemberFlagMessage.Patron),
            IsEarlyUser = firstSeen.FirstSeen.ToDateTimeOffset() <
                          new DateTimeOffset(2020, 9, 1, 0, 0, 0, TimeSpan.Zero),
            ProfileImageUrl = context.User.AvatarUrl,
            SpritesCount = spriteInv.Count,
            ServersConnected = member.ServerConnections.Count,
            FirstSeen = $"{firstSeen.FirstSeen.ToDateTimeOffset():d}",
            Bubbles = member.Bubbles,
            Drops = dropCredit.TotalCredit,
            BubbleRank = bubbleRank,
            DropRank = dropRank,
            DropRatio = dropCredit.TotalCredit /
                        Math.Max(1, member.Bubbles / 1000d), // TODO add total value incl event drops
            EventsParticipated = 0,
            Combo = new GenerateComboMessage { SpriteIds = { combo }, ColorMaps = { colorMaps } }
        }).CollectFileChunksAsync();

        var message = new DiscordMessageBuilder()
            .AddFile(card.FileName, card.Data);

        await context.RespondAsync(message);
    }
}