using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using tobeh.Palantir.Commands.Checks;
using tobeh.Palantir.Commands.Extensions;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Commands.Commands;

/// <summary>
/// Change the settings of the bot on this server
/// </summary>
/// <param name="logger"></param>
/// <param name="guildsClient"></param>
[Command("server"), TextAlias("sv"), RequireGuild, RequireServerHome]
public class ServerCommands(
    ILogger<ServerCommands> logger,
    Guilds.GuildsClient guildsClient,
    Members.MembersClient membersClient,
    MemberContext memberContext,
    ServerHomeContext serverHomeContext)
{
    /// <summary>
    /// Change the prefix of the bot on this server
    /// This affects ony the lobby bot
    /// </summary>
    /// <param name="context"></param>
    /// <param name="prefix">The new prefix for text commands on this server</param>
    [Command("prefix"), TextAlias("pf"), RequirePermissions(DiscordPermissions.None, DiscordPermissions.Administrator)]
    public async Task SetPrefix(CommandContext context, string prefix)
    {
        logger.LogTrace("SetPrefix(prefix={prefix})", prefix);

        if (string.IsNullOrWhiteSpace(prefix))
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Invalid Prefix",
                "The prefix must not be empty."));
        }

        var currentOptions = serverHomeContext.Server;

        currentOptions.Prefix = prefix.Trim();
        currentOptions.Name = context.Guild!.Name;
        await guildsClient.SetGuildOptionsAsync(currentOptions);

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"Bot Prefix Updated")
            .WithDescription(
                $"You can now use the bot commands with `{prefix}` in front of the command names.\nIt can take a few seconds until the changes take effect.");

        await context.RespondAsync(embed);
    }

    /// <summary>
    /// Select the channel where to list lobbies of this server
    /// Only servers who have a lobby bot can show lobbies.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="channel">The channel where the bot will list lobbies. Leave empty to stop the lobby updates.</param>
    [Command("lobbies"), TextAlias("ch"), RequirePermissions(DiscordPermissions.None, DiscordPermissions.Administrator)]
    public async Task SetLobbyChannel(CommandContext context, DiscordChannel? channel)
    {
        logger.LogTrace("SetLobbyChannel(channel={channel})", channel);

        var currentOptions = serverHomeContext.Server;

        currentOptions.ChannelId = (long?)channel?.Id;
        currentOptions.Name = context.Guild!.Name;
        await guildsClient.SetGuildOptionsAsync(currentOptions);

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context);

        if (channel is null)
        {
            embed.WithTitle("Lobby Channel Removed");
            embed.WithDescription("The bot will no longer list lobbies of this server.\n" +
                                  "To activate lobbies again, use the command `/server lobbies <#channel>`.");
        }
        else
        {
            embed.WithTitle("Lobby Channel Updated");
            embed.WithDescription($"The bot will now list lobbies in {channel.Mention}.\n" +
                                  "It can take a few seconds until the changes take effect.\n" +
                                  "Users can now connect to this server with the link in the lobby message, and as soon as they are playing on skribbl.io, they will be listed!\n" +
                                  "To stop refreshing lobbies, use the command `/server lobbies` without a channel.");
        }

        await context.RespondAsync(embed);
    }

    /// <summary>
    /// View details about this server's settings.
    /// </summary>
    /// <param name="context"></param>
    [Command("info"), TextAlias("if")]
    public async Task ViewServerDetails(CommandContext context)
    {
        logger.LogTrace("ViewServerDetails()");

        var currentOptions = serverHomeContext.Server;
        var info = await guildsClient.GetGuildByIdAsync(new GetGuildByIdMessage { DiscordId = currentOptions.GuildId });

        var supporters = await membersClient
            .GetMembersByLogin(new GetMembersByLoginMessage { Logins = { info.Supporters } }).ToListAsync();
        var supportersList = string.Join(", ", supporters.Select(s => s.Username));

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle("Server Info")
            .WithDescription(
                $"This server is a typo home server.\nYou can find out more about it in {"this help article".AsTypoLink("https://www.typo.rip/help/lobby-bot", "📑")}.")
            .AddField("Supporters", $"`🫂` {supporters.Count} server supporters: {supportersList}")
            .AddField("Prefix", $"`💬` You can use all commands with the prefix `{currentOptions.Prefix}`")
            .AddField("Connection Invite",
                $"`🏠` People can connect their typo account with the command `/server connect` or using {"this invite".AsTypoLink("https://www.typo.rip/invite/" + currentOptions.Invite, "🌍")}.")
            .AddField("Lobby Channel", currentOptions.ChannelId is null
                ? "`📃` No channel set"
                : $"`📃` Lobbies are listed in <#{currentOptions.ChannelId}>");

        await context.RespondAsync(embed);
    }

    /// <summary>
    /// Connect your typo account to this server home.
    /// </summary>
    /// <param name="context"></param>
    [Command("connect"), TextAlias("cn"), RequirePalantirMember]
    public async Task ConnectToServer(CommandContext context)
    {
        logger.LogTrace("ConnectToServer()");

        var currentOptions = serverHomeContext.Server;
        var member = memberContext.Member;

        if (member.ServerConnections.Contains(currentOptions.Invite))
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Already Connected",
                "You are already connected to this server home.\n" +
                "You can disconnect with the command `/server disconnect`."));
            return;
        }

        await membersClient.AddMemberServerConnectionAsync(new ModifyServerConnectionRequest
            { ServerToken = currentOptions.Invite, Login = member.Login });

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle("Connected to Server")
            .WithDescription(
                $"You are now connected to this server home!\n" +
                $"As soon as you start playing on skribbl, you will show up in <#{currentOptions.ChannelId}>");
        await context.RespondAsync(embed);
    }

    /// <summary>
    /// Disconnect your typo account from this server home.
    /// </summary>
    /// <param name="context"></param>
    [Command("disconnect"), TextAlias("dc"), RequirePalantirMember]
    public async Task DisconnectFromServer(CommandContext context)
    {
        logger.LogTrace("DisconnectFromServer()");

        var currentOptions = serverHomeContext.Server;
        var member = memberContext.Member;

        if (!member.ServerConnections.Contains(currentOptions.Invite))
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Not Connected",
                "You are not connected to this server home.\n" +
                "You can connect with the command `/server connect`."));
            return;
        }

        await membersClient.RemoveMemberServerConnectionAsync(new ModifyServerConnectionRequest
            { ServerToken = currentOptions.Invite, Login = member.Login });

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle("Disconnected from Server")
            .WithDescription(
                $"You are now no longer connected to this server home.\n" +
                $"To connect again, use the command `/server connect`.");
        await context.RespondAsync(embed);
    }
}