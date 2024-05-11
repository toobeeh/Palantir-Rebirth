using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using tobeh.Palantir.Commands.Checks;
using tobeh.Palantir.Commands.Extensions;
using tobeh.Valmar;

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
}