using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands.Attributes;
using DSharpPlus.Commands.Trees.Attributes;

namespace Palantir_Commands.Discord.Commands;

[Command("dev")]
public class DevelopmentCommands
{
    [DefaultGroupCommand]
    public static async Task GroupCommand(CommandContext context)
    {
        await context.RespondAsync("general kenobi");
    }

    [Command("test")]
    public async Task TestCommand(CommandContext context)
    {
        await context.RespondAsync($"hello there! {context.Extension.Commands.Count} commands registered");
    }
    
}