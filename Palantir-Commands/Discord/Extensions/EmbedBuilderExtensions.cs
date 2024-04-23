using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using Palantir_Commands.Services;

namespace Palantir_Commands.Discord.Extensions;

public static class EmbedBuilderExtensions
{
    public static DiscordEmbedBuilder WithPalantirPresets(this DiscordEmbedBuilder builder, CommandContext context)
    {
        var name = context.Member?.DisplayName ?? context.User.Username;

        string? akaPalantir;
        try
        {
            var memberContext = context.ServiceProvider.GetRequiredService<MemberContext>();
            akaPalantir = memberContext.Member.Username == name ? null : $" (👤 {memberContext.Member.Username})";
        }
        catch
        {
            akaPalantir = null;
        }
        
        builder.WithColor(DiscordColor.CornflowerBlue);
        builder.WithFooter($"{name}{akaPalantir}", "https://i.imgur.com/Smt9vsr.png");
        builder.WithTimestamp(DateTimeOffset.Now);
        return builder;
    }
    
    public static DiscordEmbedBuilder WithPalantirErrorPresets(this DiscordEmbedBuilder builder, CommandContext context, string? errorTitle = null, string? errorDescription = null)
    {
        var name = context.Member?.DisplayName ?? context.User.Username;

        string? akaPalantir;
        try
        {
            var memberContext = context.ServiceProvider.GetRequiredService<MemberContext>();
            akaPalantir = memberContext.Member.Username == name ? null : $" aka {memberContext.Member.Username}";
        }
        catch
        {
            akaPalantir = null;
        }

        if (errorTitle is not null) builder.WithTitle(errorTitle);
        if (errorDescription is not null) builder.WithDescription(errorDescription);
        builder.WithColor(DiscordColor.IndianRed);
        builder.WithAuthor("Command Error");
        builder.WithFooter($"{name}{akaPalantir}", "https://i.imgur.com/Smt9vsr.png");
        builder.WithTimestamp(DateTimeOffset.Now);
        return builder;
    }
    
    public static DiscordEmbedBuilder WithDualColumnFields<TItem>(this DiscordEmbedBuilder builder, IList<TItem> items, Func<TItem, string> titleSelector, Func<TItem, string> contentSelector)
    {
        var remaining = items.ToList();
        while (remaining.Count > 0)
        {
            var seq = remaining.Take(2).ToList();
            remaining = remaining.Skip(2).ToList();
            
            seq.ForEach(item => builder.AddField(titleSelector.Invoke(item), contentSelector.Invoke(item), true));
            if(remaining.Count > 0) builder.AddField("_ _", "_ _");
        }

        return builder;
    }
    
    public static DiscordMessageBuilder ToMessageBuilderWithAttachmentImage(this DiscordEmbedBuilder builder, string fileName, Stream fileStream)
    {
        builder.WithImageUrl($"attachment://{fileName}");
        
        var messageBuilder = new DiscordMessageBuilder();
        messageBuilder.AddEmbed(builder);
        messageBuilder.AddFile(fileName, fileStream);
        return messageBuilder;
    }
}