using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Palantir_Commands.Discord.Checks;
using Palantir_Commands.Discord.Extensions;
using Palantir_Commands.Services;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace Palantir_Commands.Discord.Commands;

[Command("award"), TextAlias("aw")]
public class AwardCommands(
    ILogger<AwardCommands> logger,
    MemberContext memberContext,
    Awards.AwardsClient awardsClient,
    Inventory.InventoryClient inventoryClient,
    Members.MembersClient membersClient
)
{
    private string GetAwardEmote(AwardRarityMessage rarity) => rarity switch
    {
        AwardRarityMessage.Common => TypoEmotes.CommonAward,
        AwardRarityMessage.Special => TypoEmotes.SpecialAward,
        AwardRarityMessage.Epic => TypoEmotes.EpicAward,
        _ => TypoEmotes.LegendaryAward
    };

    /// <summary>
    /// Lists the award inventory
    /// </summary>
    /// <param name="context"></param>
    [Command("inventory"), TextAlias("inv"), DefaultGroupCommand, RequirePalantirMember(MemberFlagMessage.Beta)]
    public async Task ViewAwardInventory(CommandContext context)
    {
        logger.LogTrace("ViewAwardInventory(context)");

        var member = memberContext.Member;
        var awards = await awardsClient.GetAllAwards(new Empty()).ToListAsync();
        var awardsDict = awards.ToDictionary(award => award.Id);
        var inventory =
            await inventoryClient.GetAwardInventoryAsync(new GetAwardInventoryMessage { Login = member.Login });
        var awardPackLevel =
            await inventoryClient.GetAwardPackLevelAsync(new GetAwardPackLevelMessage { Login = member.Login });

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle("Award Inventory")
            .WithDescription("Awards are items that you can give to special drawings on skribbl.\n" +
                             "The person who receives the award will see it in their gallery.\n" +
                             "You can view your gallery with the command `/award gallery`.");

        var receivedList = inventory.ReceivedAwards
            .Select(award => awardsDict[award.AwardId])
            .GroupBy(award => award.Rarity)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}: x{group.Count()}");

        var givenGrouping = inventory.GivenAwards
            .Select(award => awardsDict[award.AwardId])
            .GroupBy(award => award.Rarity)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}: x{group.Count()}");

        embed.AddField("Received Awards", $"```js\n{string.Join("\n", receivedList)}\n```", true);
        embed.AddField("Given Awards", $"```js\n{string.Join("\n", givenGrouping)}\n```", true);

        embed.AddField("**Available Awards**",
            "When a palantir member draws on skribbl, you can click the star icon and choose one of your available awards to gift it to the drawer.");

        var rarities = inventory.AvailableAwards
            .Select(award => awardsDict[award.AwardId])
            .GroupBy(award => award.Rarity)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var emote = GetAwardEmote(group.Key);

                var desc = group
                    .GroupBy(award => award.Id)
                    .Select(group => new { Award = group.First(), Count = group.Count() })
                    .Select(award => $"{award.Award.Name}: x{award.Count}");

                return new
                {
                    Title = $"{emote} {group.Key}",
                    Description = $"```js\n{string.Join("\n", desc)}\n```"
                };
            })
            .ToList();

        // add embed fields so that there are only two inline next to each other
        embed.WithDualColumnFields(rarities, rarity => rarity.Title, rarity => rarity.Description);

        // add award pack info
        var nextPackInfo = member.NextAwardPackDate.ToDateTimeOffset() > DateTimeOffset.UtcNow
            ? $"{Formatter.Timestamp(member.NextAwardPackDate.ToDateTimeOffset(), TimestampFormat.ShortDateTime)}"
            : "You can open a new pack now!";

        embed.AddField("Award Pack",
            $"You can get new awards every 7 (for patrons: 5) days by opening an award pack.\n" +
            $"The more bubbles you have collected in the last week, the higher your award pack level is!\n" +
            $"\n`✨` **Award Pack Level:**  {awardPackLevel.Level} ({awardPackLevel.CollectedBubbles} bubbles)\n" +
            $"`🎟️` **Next Pack:**  {nextPackInfo}");

        var openPackEnabled = member.NextAwardPackDate.ToDateTimeOffset() <= DateTimeOffset.UtcNow;
        if (!openPackEnabled)
        {
            await context.RespondAsync(embed: embed);
            return;
        }

        var message = new DiscordMessageBuilder().AddEmbed(embed);
        var openBtn = new DiscordButtonComponent(DiscordButtonStyle.Primary, "open", "Open Award Pack", false,
            new DiscordComponentEmoji("✨"));
        message.AddComponents(openBtn);

        await context.RespondAsync(message);
        var sent = await context.GetResponseAsync();

        if (sent is null)
        {
            throw new Exception("Could not get response");
        }

        var result = await context.Client.GetInteractivity()
            .WaitForButtonAsync(sent, context.User, TimeSpan.FromMinutes(1));
        if (!result.TimedOut)
        {
            await result.Result.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
            var newAwards = await inventoryClient.OpenAwardPackAsync(new OpenAwardPackMessage { Login = member.Login });

            var response = new DiscordFollowupMessageBuilder();
            foreach (var award in newAwards.Awards)
            {
                response.AddEmbed(new DiscordEmbedBuilder().WithPalantirPresets(context)
                    .WithTitle($"You pulled a **{award.Name}**")
                    .WithThumbnail(award.Url)
                    .WithDescription(
                        $"{award.Description}\n_ _\n{GetAwardEmote(award.Rarity)} {award.Rarity} Award Rarity"));
            }

            await result.Result.Interaction.CreateFollowupMessageAsync(response);
        }

        message.ClearComponents();
        await sent.ModifyAsync(message);
    }

    /// <summary>
    /// Views a listing of all  your awarded images
    /// </summary>
    /// <param name="context"></param>
    [Command("gallery"), TextAlias("gl"), RequirePalantirMember]
    public async Task ViewAwardGallery(CommandContext context)
    {
        logger.LogTrace("ViewAwardGallery(context)");

        var member = memberContext.Member;
        var awards = await awardsClient.GetAllAwards(new Empty()).ToListAsync();
        var awardsDict = awards.ToDictionary(award => award.Id);
        var inventory =
            await inventoryClient.GetAwardInventoryAsync(new GetAwardInventoryMessage { Login = member.Login });

        var imageIds = inventory.ReceivedAwards.Select(award => award.LinkedImageId).OfType<long>();
        var images = await inventoryClient
            .GetGalleryItems(new GetGalleryItemsMessage { ImageIds = { imageIds }, Login = member.Login })
            .ToListAsync();
        var imageDict = images.ToDictionary(image => image.ImageId);

        var awardOwnerLogins = inventory.ReceivedAwards.Select(award => award.OwnerLogin).Distinct().ToList();
        var awardOwners = await membersClient
            .GetMembersByLogin(new GetMembersByLoginMessage { Logins = { awardOwnerLogins } }).ToListAsync();
        var ownersDict = awardOwners.ToDictionary(other => other.Login);

        var gallery = inventory.ReceivedAwards
            .OrderByDescending(award => award.AwardedTimestamp)
            .Select(award => new
            {
                Awardee = award, Award = awardsDict[award.AwardId],
                Image = award.LinkedImageId is { } id ? imageDict[id] : null
            })
            .Select((item, index) =>
            {
                var emote = GetAwardEmote(item.Award.Rarity);

                return new
                {
                    Title = $"{(index + 1).AsTypoId()}  {item.Award.Name} _ _ {emote}",
                    Description = $"> {item.Image?.Title ?? "Unknown Image"}\n" +
                                  $"From: {ownersDict[item.Awardee.OwnerLogin].Username}\n" +
                                  $"On: {item.Awardee.AwardedTimestamp.ToDateTimeOffset():d}"
                };
            });

        const int pageSize = 8;
        var pages = gallery.Chunk(pageSize).Select(page =>
        {
            var embed = new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithTitle("Award Gallery")
                .WithDescription("Here you can see all the awards you have received from other players.\n" +
                                 "To view a single award and its image, use the command `/award view (id)`.")
                .WithDualColumnFields(page, item => item.Title, item => $"```md\n{item.Description}\n```");

            return embed;
        }).ToList();

        await context.RespondPalantirPaginationAsync(pages);
    }

    /// <summary>
    /// Views an awarded image from your gallery
    /// </summary>
    /// <param name="context"></param>
    /// <param name="id">The image id from the gallery</param>
    [Command("view"), TextAlias("vw"), RequirePalantirMember]
    public async Task ViewAwardImage(CommandContext context, uint id)
    {
        logger.LogTrace("ViewAwardImage(context, id={id})", id);

        var member = memberContext.Member;
        var inventory =
            await inventoryClient.GetAwardInventoryAsync(new GetAwardInventoryMessage { Login = member.Login });
        var target = inventory
            .ReceivedAwards
            .OrderByDescending(award => award.AwardedTimestamp)
            .ElementAt((int)id - 1);
        var award = await awardsClient.GetAwardByIdAsync(new GetAwardRequest { Id = target.AwardId });

        if (target.LinkedImageId is null)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context, "No image available :/",
                    "This can happen when the drawer leaves the lobby before the drawing is finished."));
            return;
        }

        var awardOwner = await membersClient.GetMemberByLoginAsync(new IdentifyMemberByLoginRequest
            { Login = target.OwnerLogin });
        var image = (await inventoryClient
            .GetGalleryItems(new GetGalleryItemsMessage
                { ImageIds = { target.LinkedImageId.Value }, Login = member.Login })
            .ToListAsync()).First();
        var emote = GetAwardEmote(award.Rarity);

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor("Viewing an awarded image")
            .WithTitle($"{image.Title} by  {image.Author}")
            .WithThumbnail(award.Url)
            .WithImageUrl(image.ImageUrl)
            .WithDescription($"{emote} {award.Name}\n" +
                             $"```asciidoc\n{award.Description}\n - {awardOwner.Username} on {target.AwardedTimestamp.ToDateTimeOffset():d}```");

        await context.RespondAsync(embed: embed);
    }
}