using DSharpPlus.Commands;
using tobeh.Palantir.Commands.Checks;
using tobeh.Valmar;

namespace tobeh.Palantir.Commands.Commands;

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