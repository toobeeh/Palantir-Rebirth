using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Entities;

namespace Palantir_Commands.Discord.Extensions;

public static class EmbedBuilderExtensions
{
    public static DiscordEmbedBuilder WithPalantirPresets(this DiscordEmbedBuilder builder, CommandContext context)
    {
        var name = context.Member?.DisplayName ?? context.User.Username;
        
        builder.WithColor(DiscordColor.CornflowerBlue);
        builder.WithFooter($"{name}", "https://i.imgur.com/Smt9vsr.png");
        builder.WithTimestamp(DateTimeOffset.Now);
        return builder;
    }
    
    public static DiscordEmbedBuilder WithPalantirErrorPresets(this DiscordEmbedBuilder builder, CommandContext context, string? errorTitle = null, string? errorDescription = null)
    {
        var name = context.Member?.DisplayName ?? context.User.Username;

        if (errorTitle is not null) builder.WithTitle(errorTitle);
        if (errorDescription is not null) builder.WithDescription(errorDescription);
        builder.WithColor(DiscordColor.IndianRed);
        builder.WithAuthor("Command Error");
        builder.WithFooter($"{name}", "https://i.imgur.com/Smt9vsr.png");
        builder.WithTimestamp(DateTimeOffset.Now);
        return builder;
    }
}