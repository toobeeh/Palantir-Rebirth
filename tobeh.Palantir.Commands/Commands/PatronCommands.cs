using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using tobeh.Palantir.Commands.Checks;
using tobeh.Palantir.Commands.Extensions;
using tobeh.Valmar;

namespace tobeh.Palantir.Commands.Commands;

/// <summary>
/// Commands to manage patreon subscription features
/// </summary>
/// <param name="logger"></param>
/// <param name="memberContext"></param>
/// <param name="inventoryClient"></param>
[Command("patron")]
public class PatronCommands(
    ILogger<PatronCommands> logger,
    MemberContext memberContext,
    Workers.WorkersClient workersClient,
    Inventory.InventoryClient inventoryClient)
{
    /// <summary>
    /// Choose an emoji that will be shown instead of your bubbles in palantir
    /// </summary>
    /// <param name="context"></param>
    /// <param name="emoji">The emoji of your choice</param>
    [Command("emoji"), RequirePalantirMember(MemberFlagMessage.Patron)]
    public async Task SetPatronEmoji(CommandContext context, DiscordEmoji? emoji = null)
    {
        logger.LogTrace("SetPatronEmoji(emoji={emoji})", emoji);

        var member = memberContext.Member;

        await inventoryClient.SetPatronEmojiAsync(new SetPatronEmojiMessage
            { Login = member.Login, Emoji = emoji?.ToString() });

        if (emoji is not null)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithAuthor("Prettyyyy!")
                .WithTitle($"Emoji updated to `{emoji}`")
                .WithDescription(
                    $"This emoji will be displayed next to your score in the Palantir lobby message on Discord.\n" +
                    $"You can use the command `/patron emoji` to remove it."));
        }
        else
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithAuthor("You're a commoner now")
                .WithTitle($"Emoji removed")
                .WithDescription(
                    $"You removed your patron emoji.\n" +
                    $"You can use the command `/patron emoji [emoji]` to choose a new one."));
        }
    }

    /// <summary>
    /// Gift patreon perks to another member
    /// </summary>
    /// <param name="context"></param>
    /// <param name="user">The member that will receive your gift</param>
    [Command("gift"), RequirePalantirMember(MemberFlagMessage.Patronizer)]
    public async Task ChoosePatronize(CommandContext context, DiscordUser? user = null)
    {
        logger.LogTrace("ChoosePatronize(user={user})", user);

        var member = memberContext.Member;

        if (member.NextPatronizeDate.ToDateTimeOffset() > DateTimeOffset.UtcNow)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context)
                .WithTitle("Patronizer cooldown")
                .WithDescription($"You can only change the patronized member every seven days. \n" +
                                 $"You need to wait until {Formatter.Timestamp(member.NextPatronizeDate.ToDateTimeOffset(), TimestampFormat.ShortDateTime)}."));
            return;
        }

        await inventoryClient.PatronizeMemberAsync(new PatronizeMemberMessage
            { Login = member.Login, PatronizedDiscordId = (long?)user?.Id });

        if (user is not null)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithAuthor("Thanks for supporting typo <3")
                .WithTitle($"You have patronized your special someone!")
                .WithDescription(
                    $"{Formatter.Mention(user, true)} will be a patron as long as you have the patronizer subscription.\n" +
                    $"You can use the command `/patron gift` to remove it, or `/patron gift (@user)` to choose a new receiver.\n" +
                    $"Patronized members can be switched every seven days."));
        }
        else
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithTitle($"Patronized member removed")
                .WithDescription(
                    $"You are now patronizing no one.\n" +
                    $"To choose someone, use the command `/patron gift (@user)`."));
        }
    }

    /// <summary>
    /// Select this server as your home server and unlock the lobbies bot
    /// </summary>
    /// <param name="context"></param>
    [Command("home"), RequirePalantirMember(MemberFlagMessage.Patron), RequireGuild]
    public async Task ChooseHome(CommandContext context)
    {
        logger.LogTrace("ChooseHome()");

        var member = memberContext.Member;

        if (member.NextHomeChooseDate.ToDateTimeOffset() > DateTimeOffset.UtcNow)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithPalantirErrorPresets(context)
                .WithTitle("Home server cooldown")
                .WithDescription($"You can only set a home server every seven days. \n" +
                                 $"You need to wait until {Formatter.Timestamp(member.NextHomeChooseDate.ToDateTimeOffset(), TimestampFormat.ShortDateTime)} to select another server."));
            return;
        }

        var instance = await workersClient.AssignInstanceToServerAsync(new AssignInstanceToServerMessage
        {
            Login = member.Login,
            ServerId = (long)(context.Guild ?? throw new Exception("command was called without a guild")).Id
        });

        var invite = $"https://discord.com/oauth2/authorize?client_id={instance.BotId}&scope=bot&permissions=604310528";

        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithAuthor("Thanks for supporting typo <3")
            .WithTitle($"You are now supporting this server!")
            .WithDescription(
                $"This server is now able to be used as typo server home.\n" +
                $"A server admin can now {"add the lobby bot to the server".AsTypoLink(invite, "✨")}. \n\n" +
                $"To set up the all features of a typo server home, have a look at {"this article".AsTypoLink("https://www.typo.rip/help/lobby-bot", "📑")}."));
    }
}