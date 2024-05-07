using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using tobeh.Palantir.Commands.Extensions;
using tobeh.Valmar;

namespace tobeh.Palantir.Commands.Commands;

/// <summary>
/// Change the settings of the bot on this server
/// </summary>
/// <param name="logger"></param>
/// <param name="memberContext"></param>
/// <param name="guildsClient"></param>
[Command("server"), TextAlias("sv")]
public class ServerCommands(
    ILogger<ServerCommands> logger,
    MemberContext memberContext,
    Guilds.GuildsClient guildsClient,
    Workers.WorkersClient workersClient)
{
    /// <summary>
    /// Change the prefix of the bot on this server
    /// This affects ony the lobby bot
    /// </summary>
    /// <param name="context"></param>
    /// <param name="prefix">The new prefix for text commands on this server</param>
    [Command("prefix"), TextAlias("pf"), RequireGuild,
     RequirePermissions(DiscordPermissions.None, DiscordPermissions.Administrator)]
    public async Task SetPrefix(CommandContext context, string prefix)
    {
        logger.LogTrace("SetPrefix(prefix={prefix})", prefix);

        if (string.IsNullOrWhiteSpace(prefix))
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Invalid Prefix",
                "The prefix must not be empty."));
        }

        GuildOptionsMessage currentOptions;

        try
        {
            currentOptions = await workersClient.GetGuildOptionsByIdAsync(new GetGuildOptionsByIdMessage
                { GuildId = (long)context.Guild!.Id });
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "This server has no Lobby Bot",
                "To add the Lobby Bot, a Patreon subscriber can choose this server as their home server with the command `/patron home`."));
            return;
        }

        currentOptions.Prefix = prefix.Trim();
        currentOptions.Name = context.Guild!.Name;
        await workersClient.SetGuildOptionsAsync(currentOptions);

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
    [Command("lobbies"), TextAlias("ch"), RequireGuild,
     RequirePermissions(DiscordPermissions.None, DiscordPermissions.Administrator)]
    public async Task SetLobbyChannel(CommandContext context, DiscordChannel? channel)
    {
        logger.LogTrace("SetLobbyChannel(channel={channel})", channel);

        GuildOptionsMessage currentOptions;

        try
        {
            currentOptions = await workersClient.GetGuildOptionsByIdAsync(new GetGuildOptionsByIdMessage
                { GuildId = (long)context.Guild!.Id });
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "This server has no dedicated bot",
                "You can only change the prefix of the Palantir Lobby Bot.\n" +
                "To get the lobby bot, a patron user can choose this server as their home with the command `/patron home`."));
            return;
        }

        // check if the channel has the right permissions
        /*if (channel is not null && context.Guild is not null && context.Member is not null)
        {
            var botPermissions = channel.PermissionsFor(context.Member!);

            DiscordPermissions[] requiredPermissionsBot = [DiscordPermissions.SendMessages, DiscordPermissions.ReadMessageHistory];
            if (!requiredPermissionsBot.All(p => botPermissions.HasPermission(p)))
            {
                await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                    "The channel has wrong permissions",
                    "Make sure that the Lobby Bot can .\n" +
                    "To get the lobby bot, a patron user can choose this server as their home with the command `/patron home`."));
                return;
            }

            var everyonePermissions = channel.PermissionsFor(context.Guild.EveryoneRole);
            DiscordPermissions[] blacklistedPermissions = [DiscordPermissions.SendMessages];
            if (blacklistedPermissions.Any(p => everyonePermissions.HasPermission(p)))
            {

            }
        }*/

        currentOptions.ChannelId = (long?)channel?.Id;
        currentOptions.Name = context.Guild!.Name;
        await workersClient.SetGuildOptionsAsync(currentOptions);

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
}