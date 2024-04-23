using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees.Attributes;
using Palantir_Commands.Discord.Checks;
using tobeh.Valmar;

namespace Palantir_Commands.Discord.Commands;

[Command("dev")]
[RequirePalantirMember(MemberFlagMessage.Moderator)]
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