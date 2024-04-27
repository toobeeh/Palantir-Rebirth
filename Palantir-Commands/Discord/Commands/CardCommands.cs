using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Palantir_Commands.Discord.Checks;
using Palantir_Commands.Discord.Extensions;
using Palantir_Commands.Services;
using tobeh.TypoImageGen;
using tobeh.TypoImageGen.Client.Util;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace Palantir_Commands.Discord.Commands;

/// <summary>
/// View your personalized card or change card settings
/// </summary>
/// <param name="logger"></param>
/// <param name="memberContext"></param>
/// <param name="inventoryClient"></param>
/// <param name="eventsClient"></param>
/// <param name="cardClient"></param>
/// <param name="imageGeneratorClient"></param>
/// <param name="statsClient"></param>
[Command("card")]
public class CardCommands(
    ILogger<CardCommands> logger,
    MemberContext memberContext,
    Inventory.InventoryClient inventoryClient,
    Events.EventsClient eventsClient,
    Card.CardClient cardClient,
    ImageGenerator.ImageGeneratorClient imageGeneratorClient,
    Stats.StatsClient statsClient
)
{
    /// <summary>
    /// View your personalized card with your stats and sprites
    /// </summary>
    /// <param name="context"></param>
    [Command("view"), TextAlias("vw"), DefaultGroupCommand, RequirePalantirMember(MemberFlagMessage.Patron),
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
        var totalEventDropValue = dropCredit.LeagueEventDropValues.Sum(drop => drop.Value);
        var eventDrops = await eventsClient.GetAllEventDrops(new Empty()).ToListAsync();
        var eventsParticipated = eventDrops
            .Where(drop => dropCredit.LeagueEventDropValues.Any(credit => credit.EventDropId == drop.Id))
            .Select(drop => drop.EventId).Distinct().Count();

        var totalDrops = dropCredit.TotalCredit + totalEventDropValue;

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
            DropRatio = totalDrops /
                        Math.Max(1, member.Bubbles / 1000d),
            EventsParticipated = eventsParticipated,
            EventsDropValue = (int)totalEventDropValue,
            Combo = new GenerateComboMessage { SpriteIds = { combo }, ColorMaps = { colorMaps } }
        }).CollectFileChunksAsync();

        var message = new DiscordMessageBuilder()
            .AddFile(card.FileName, card.Data);

        await context.RespondAsync(message);
    }

    /// <summary>
    /// Customize your card with a theme color, text colors, background image, and more
    /// </summary>
    /// <param name="context"></param>
    /// <param name="themeColor">The primary color of your card</param>
    /// <param name="lightTextColor">The color of the primary text</param>
    /// <param name="darkTextColor">The color of the secondary text</param>
    /// <param name="imgurBackgroundId">The background image ID, can be obtained with the card customize tool on the typo website.</param>
    /// <param name="backgroundOpacity">The opacity of the card's background image</param>
    /// <param name="headerOpacity">The opacity of the card's header</param>
    /// <param name="cardTemplate">The card style - currently "classic" or "froggo" are available</param>
    [Command("customize"), TextAlias("cm"), RequirePalantirMember(MemberFlagMessage.Patron)]
    public async Task SetCardPresets(CommandContext context, string themeColor = "black",
        string lightTextColor = "white", string darkTextColor = "white", string? imgurBackgroundId = null,
        double backgroundOpacity = 0.7, double headerOpacity = 1, string cardTemplate = "classic")
    {
        var member = memberContext.Member;

        var settings = new SetMemberCardSettingsMessage
        {
            Login = member.Login,
            Settings = new MemberCardSettingsMessage
            {
                TemplateName = cardTemplate,
                HeaderColor = themeColor,
                HeaderOpacity = headerOpacity,
                BackgroundOpacity = backgroundOpacity,
                BackgroundImage = imgurBackgroundId,
                LightTextColor = lightTextColor,
                DarkTextColor = darkTextColor
            }
        };

        await cardClient.SetMemberCardSettingsAsync(settings);

        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle("Updated card settings")
            .WithDescription("Your card settings have been updated.\n" +
                             " You can now view your card with `/card view`!"));
    }
}