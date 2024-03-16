using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees.Attributes;

namespace Palantir_Commands.Discord.Commands;

public class DevelopmentCommands
{

    [Command("test")]
    public async Task TestCommand(CommandContext context, int number)
    {
        await context.RespondAsync("hello there!\n" + number);
    }
    
}