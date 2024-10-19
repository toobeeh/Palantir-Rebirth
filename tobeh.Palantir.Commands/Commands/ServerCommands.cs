using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
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
    Workers.WorkersClient workersClient,
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
    /// Customize the name of the Lobby Bot on the server
    /// </summary>
    /// <param name="context"></param>
    /// <param name="name">The new name for the LObby Bot. Empty to use the default name.</param>
    [Command("botname"), TextAlias("bn"), RequirePermissions(DiscordPermissions.None, DiscordPermissions.Administrator)]
    public async Task SetBotName(CommandContext context, string? name = null)
    {
        logger.LogTrace("SetBotName(name={name})", name);

        if (name?.Length is 0 or > 20)
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Invalid name",
                "The name must not be empty or longer than 20 characters."));
        }

        var currentOptions = serverHomeContext.Server;

        currentOptions.BotName = name;
        currentOptions.Name = context.Guild!.Name;
        await guildsClient.SetGuildOptionsAsync(currentOptions);

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"Bot Name Updated")
            .WithDescription(
                $"The Lobby Bot will now use your custom name!\nIt can take a few seconds until the changes take effect.");

        await context.RespondAsync(embed);
    }

    /// <summary>
    /// Select the channel where to list lobbies of this server
    /// Only servers who have a lobby bot can show lobbies.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="channel">The channel where the bot will list lobbies. Leave empty to stop the lobby updates.</param>
    [Command("lobbies"), TextAlias("ch"), RequirePermissions(DiscordPermissions.None, DiscordPermissions.Administrator)]
    public async Task SetLobbyChannel(CommandContext context, DiscordChannel? channel = null)
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
        var webhooks = await guildsClient
            .GetGuildWebhooks(new GetGuildWebhooksMessage { GuildId = currentOptions.GuildId }).ToListAsync();
        var containerId = Environment.GetEnvironmentVariable("HOSTNAME");
        var invite =
            $"https://discord.com/oauth2/authorize?client_id={info.BotId}&scope=bot&permissions=604310528";

        var supporters = await membersClient
            .GetMembersByLogin(new GetMembersByLoginMessage { Logins = { info.Supporters } }).ToListAsync();
        var supportersList = string.Join(", ", supporters.Select(s => s.Username));

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle("Server Info")
            .WithDescription(
                $"This server is a typo home server.\nYou can find out more about it in {"this help article".AsTypoLink("https://www.typo.rip/help/lobby-bot", "📑")}.")
            .AddField("Supporters", $"`🫂` {supporters.Count} server supporters: {supportersList}")
            .AddField("Members", $"`🔗` {info.ConnectedMemberCount} members connected")
            .AddField("Prefix", $"`💬` You can use all commands with the prefix `{currentOptions.Prefix}`")
            .AddField("Bot Name",
                currentOptions.BotName is null
                    ? $"`🤖` The Lobby Bot has the default name"
                    : $"`🤖` The Lobby Bot is named `{currentOptions.BotName}`")
            .AddField("Bot Invite",
                $"If the bot is not yet on your server, you can invite it with {"this link".AsTypoLink(invite, "🌍")}")
            .AddField("Connection Invite",
                $"`🏠` People can connect their typo account with the command `/server connect` or using {"this invite".AsTypoLink("https://www.typo.rip/invite/" + currentOptions.Invite, "🌍")}.")
            .AddField("Lobby Channel", currentOptions.ChannelId is null
                ? "`📃` No channel set"
                : $"`📃` Lobbies are listed in <#{currentOptions.ChannelId}>");

        if (webhooks.Count > 0)
        {
            IReadOnlyList<DiscordWebhook>? serverWebhooks = null;
            try
            {
                serverWebhooks = await context.Guild!.GetWebhooksAsync();
            }
            catch (UnauthorizedException)
            {
                embed.AddField("Image Post Channels",
                    "⚠️ Palantir requires manage webhook permission to manage image posts.");
            }

            if (serverWebhooks is not null)
            {
                var postsWithChannel = webhooks
                    .Select(post => new
                        { Post = post, Webhook = serverWebhooks.FirstOrDefault(hook => hook.Url == post.Url) })
                    .ToList();
                embed.AddField("Image Post Channels",
                    string.Join("\n",
                        postsWithChannel.Select(post =>
                            $"- `{post.Post.Name}` in {(post.Webhook is null ? "`⚠️ Corrupted`" : $"<#{post.Webhook.ChannelId}>")}")));
            }
        }

        if (containerId is not null)
        {
            embed.AddField("_ _", "||Container ID: " + containerId + "||");
        }

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

    /// <summary>
    /// View a list of all members that have been banned from this server home.
    /// </summary>
    /// <param name="context"></param>
    [Command("bans"), RequirePalantirMember]
    public async Task ViewBannedMembers(CommandContext context)
    {
        logger.LogTrace("ViewBannedMembers()");

        var currentOptions = serverHomeContext.Server;
        var bannedMembers = await guildsClient
            .GetGuildBans(new GetGuildBansMessage { GuildId = currentOptions.GuildId }).ToListAsync();

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle("Banned Members")
            .WithDescription(
                $"Following members are banned from this server home.\n" +
                $"Banned members are not able to connect again, use the lobby links or to see lobby buttons on skribbl.\n" +
                $"To ban a member, use the command `/server ban <user-id>`.\n" +
                $"To unban a member, use the command `/server unban <user-id>`.\n" +
                (bannedMembers.Count == 0
                    ? "> No banned members found"
                    : string.Join("\n",
                        bannedMembers.Select(m => $"- {m.Username} (<@{m.DiscordId}> / ID: `{m.DiscordId}`)"))));
        await context.RespondAsync(embed);
    }

    /// <summary>
    /// Ban a member from this server home
    /// </summary>
    /// <param name="userId">The discord ID of the user to ban</param>
    /// <param name="context"></param>
    [Command("ban"), RequirePalantirMember,
     RequirePermissions(DiscordPermissions.None, DiscordPermissions.Administrator)]
    public async Task BanMember(CommandContext context, long userId)
    {
        logger.LogTrace("BanMember(userId={userId})", userId);

        var currentOptions = serverHomeContext.Server;
        var bannedMembers = await guildsClient
            .GetGuildBans(new GetGuildBansMessage { GuildId = currentOptions.GuildId }).ToListAsync();

        if (bannedMembers.Any(m => m.DiscordId == userId))
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Already Banned",
                "This member is already banned from this server home.\n" +
                $"You can unban them with the command `/server unban {userId}`."));
            return;
        }

        var member =
            await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest { Id = userId });
        await guildsClient.BanGuildMemberAsync(new BanGuildMemberMessage
            { GuildId = currentOptions.GuildId, MemberId = member.DiscordId, Ban = true });

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle("Member Banned")
            .WithDescription(
                $"{member.Username} (<@{member.DiscordId}>) has been banned from this server home.\n" +
                $"They are not able to connect again, use the lobby links or to see lobby buttons on skribbl.\n" +
                $"To unban them, use the command `/server unban {userId}`.\n" +
                $"You can view all banned members with the command `/server bans`.");
        await context.RespondAsync(embed);
    }

    /// <summary>
    /// Unban a member from this server home
    /// </summary>
    /// <param name="context"></param>
    /// <param name="userId">The discord ID of the user to unban</param>
    [Command("unban"), RequirePalantirMember,
     RequirePermissions(DiscordPermissions.None, DiscordPermissions.Administrator)]
    public async Task UnbanMember(CommandContext context, long userId)
    {
        logger.LogTrace("UnbanMember(userId={userId})", userId);

        var currentOptions = serverHomeContext.Server;
        var bannedMembers = await guildsClient
            .GetGuildBans(new GetGuildBansMessage { GuildId = currentOptions.GuildId }).ToListAsync();

        if (bannedMembers.All(m => m.DiscordId != userId))
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Not Banned",
                "This member is not banned from this server home.\n" +
                $"You can ban them with the command `/server ban {userId}`."));
            return;
        }

        var member =
            await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest { Id = userId });
        await guildsClient.BanGuildMemberAsync(new BanGuildMemberMessage
            { GuildId = currentOptions.GuildId, MemberId = member.DiscordId, Ban = false });

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle("Member Unbanned")
            .WithDescription(
                $"{member.Username} (<@{member.DiscordId}>) has been unbanned from this server home.\n" +
                $"They can now reconnect to this server.\n" +
                $"To ban them again, use the command `/server ban {userId}`.\n" +
                $"You can view all banned members with the command `/server bans`.");
        await context.RespondAsync(embed);
    }

    /// <summary>
    /// Sets whether lobby links in the lobby message should be protected.
    /// Only users who have connected to the server can use the links.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="protectEnabled">Whether to enable lobby link protection</param>
    [Command("useprotection"), TextAlias("up"), RequirePalantirMember,
     RequirePermissions(DiscordPermissions.None, DiscordPermissions.Administrator)]
    public async Task UseLinkProtectio(CommandContext context, bool protectEnabled)
    {
        logger.LogTrace("UseLinkProtectionMember(protectEnabled={protectEnabled})", protectEnabled);

        var currentOptions = serverHomeContext.Server;

        currentOptions.ProxyLinks = protectEnabled;
        currentOptions.Name = context.Guild!.Name;
        await guildsClient.SetGuildOptionsAsync(currentOptions);

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"Lobby Link Protection updated")
            .WithDescription(
                $"Lobby link protection is now {(protectEnabled ? "enabled" : "disabled")}.\n" +
                $"When protection is enabled, only users who are connected to this server can use the lobby links.\n" +
                "To make full use of protection, it is recommended to hide the server connect link using `/server showconnect true`, and display it in a channel where only verified members can see it instead.");

        await context.RespondAsync(embed);
    }

    /// <summary>
    /// Sets whether to display the typo server connection link in the lobby message.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="connectEnabled">Whether to enable server connection link in the lobby message</param>
    [Command("showconnect"), TextAlias("sc"), RequirePalantirMember,
     RequirePermissions(DiscordPermissions.None, DiscordPermissions.Administrator)]
    public async Task ShowInviteLink(CommandContext context, bool connectEnabled)
    {
        logger.LogTrace("UseLinkProtectionMember(connectEnabled={connectEnabled})", connectEnabled);

        var currentOptions = serverHomeContext.Server;

        currentOptions.ShowInvite = connectEnabled;
        currentOptions.Name = context.Guild!.Name;
        await guildsClient.SetGuildOptionsAsync(currentOptions);

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle($"Connection Link visibility updated")
            .WithDescription(
                $"The connection link is now {(connectEnabled ? "visible" : "hidden")}.\n" +
                $"When the link is invisible, it will not be shown in the lobby message.\n" +
                "This prevents that players with throwaway accounts can join the server and grab lobby links.");

        await context.RespondAsync(embed);
    }
}