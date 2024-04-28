using DSharpPlus.Commands;
using Palantir_Commands.Discord.Checks;
using tobeh.Valmar;

namespace Palantir_Commands.Discord.Commands;

/// <summary>
/// Tools for development and debugging
/// </summary>
[Command("dev")]
[RequirePalantirMember(MemberFlagMessage.Admin)]
public class DevelopmentCommands
{
    /// <summary>
    /// Reset all slash commands on typo testground
    /// </summary>
    /// <param name="context"></param>
    [Command("cmd-reset")]
    public async Task ResetCommands(CommandContext context)
    {
        await (await context.Client.GetGuildAsync(779435254225698827)).BulkOverwriteApplicationCommandsAsync([]);
        await context.Client.BulkOverwriteGlobalApplicationCommandsAsync([]);
        await context.RespondAsync($"commands have been reset.");
    }
}