using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands.Attributes;
using DSharpPlus.Commands.Trees.Attributes;

namespace Palantir_Commands.Discord.Commands;

[Command("dev")]
public class DevelopmentCommands
{

    [Command("cmd-reset")]
    public async Task ResetCommands(CommandContext context)
    {
        await (await context.Client.GetGuildAsync(779435254225698827)).BulkOverwriteApplicationCommandsAsync([]);
        await context.Client.BulkOverwriteGlobalApplicationCommandsAsync([]);
        await context.RespondAsync($"commands have been reset.");
    }
    
}