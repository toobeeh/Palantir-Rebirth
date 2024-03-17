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
}