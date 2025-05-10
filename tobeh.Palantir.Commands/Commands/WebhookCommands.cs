using DSharpPlus.Commands;
using DSharpPlus.Commands.ArgumentModifiers;
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
/// Manage image post channels of this server
/// </summary>
/// <param name="logger"></param>
/// <param name="guildsClient"></param>
[Command("imagepost"), TextAlias("ip")]
public class WebhookCommands(
    ILogger<WebhookCommands> logger,
    Guilds.GuildsClient guildsClient,
    ServerHomeContext serverHomeContext)
{
    /// <summary>
    /// Add an image post to this server. Connected members will be able to post images from skribbl directly into the chosen channel.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="name">The name of the image post channel - can be anything</param>
    /// <param name="channel">The channel where the images will be posted</param>
    [Command("add"), RequireGuild, RequireServerHome,
     RequirePermissions([DiscordPermission.ManageWebhooks], [DiscordPermission.Administrator])]
    public async Task AddWebhook(CommandContext context, DiscordChannel channel, [RemainingText] string name)
    {
        logger.LogTrace("AddWebhook(name={name}, channel={channel})", name, channel);

        var webhook = await channel.CreateWebhookAsync($"Typo Image Post '{name}'");

        await guildsClient.AddGuildWebhookAsync(new AddGuildWebhookMessage
        {
            GuildId = (long)context.Guild!.Id,
            Name = name,
            Url = webhook.Url
        });

        var embed = new DiscordEmbedBuilder()
            .WithPalantirPresets(context)
            .WithTitle("Image Post added")
            .WithDescription($"The Image Post for the channel {channel.Mention} has been added.\n" +
                             "You can now use the webhook to post images in this channel.\n" +
                             "To see all Image Posts of this server, use the command `/imagepost list`.");

        await context.RespondAsync(embed);
    }

    /// <summary>
    /// Remove an image post from this server
    /// </summary>
    /// <param name="context"></param>
    /// <param name="name">The name of the image post channel</param>
    [Command("remove"), TextAlias("rm"), RequireGuild, RequireServerHome,
     RequirePermissions([DiscordPermission.ManageWebhooks], [DiscordPermission.Administrator])]
    public async Task RemoveWebhook(CommandContext context, [RemainingText] string name)
    {
        logger.LogTrace("RemoveWebhook(name={name})", name);

        var imagePosts = await guildsClient
            .GetGuildWebhooks(new GetGuildWebhooksMessage { GuildId = (long)context.Guild!.Id })
            .ToDictionaryAsync(webhook => webhook.Name);

        if (!imagePosts.TryGetValue(name, out var targetPost))
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "Image Post not found",
                $"There is no Image Post channel with the name {name} in this server. You can see all with `/imagepost list`"));
            return;
        }

        var webhooks = await context.Guild.GetWebhooksAsync();
        var webhook = webhooks.FirstOrDefault(w => w.Url == targetPost.Url);
        if (webhook is { } addedWebhook)
        {
            await addedWebhook.DeleteAsync();
        }

        await guildsClient.RemoveGuildWebhookAsync(new RemoveGuildWebhookMessage
        {
            GuildId = (long)context.Guild!.Id,
            Name = name
        });

        await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirPresets(context)
            .WithTitle("Image Post Removed")
            .WithDescription($"The Image Post channel {targetPost.Name} has been removed."));
    }

    /// <summary>
    /// List all image posts of this server
    /// </summary>
    /// <param name="context"></param>
    [Command("list"), TextAlias("ls"), RequireGuild, RequireServerHome,
     RequirePermissions([DiscordPermission.ManageWebhooks], [DiscordPermission.Administrator])]
    public async Task ListWebhooks(CommandContext context)
    {
        logger.LogTrace("ListWebhooks()");

        var imagePosts = await guildsClient
            .GetGuildWebhooks(new GetGuildWebhooksMessage { GuildId = (long)context.Guild!.Id })
            .ToListAsync();

        var serverWebhooks = await context.Guild.GetWebhooksAsync();
        var postsWithChannel = imagePosts
            .Select(post => new { Post = post, Webhook = serverWebhooks.FirstOrDefault(hook => hook.Url == post.Url) })
            .ToList();

        await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirPresets(context)
            .WithTitle("Image Post Channels")
            .WithDescription(imagePosts.Count == 0
                ? "There are no Image Post channels in this server."
                : "This is a list of all Image Post channels of this server.\nEvery connected member can send images from skribbl to one of these channels.\n" +
                  "To remove a channel, use `/imagepost remove <name>`. You can add a new channel with `/imagepost add <#channel> <name>`\n\n" +
                  string.Join("\n",
                      postsWithChannel.Select(post =>
                          $"- `{post.Post.Name}` in {(post.Webhook is null ? "`⚠️ Corrupted`" : $"<#{post.Webhook.ChannelId}>")}"))));
    }
}