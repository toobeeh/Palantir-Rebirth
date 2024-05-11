using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using tobeh.Palantir.Commands.Extensions;
using tobeh.Valmar;

namespace tobeh.Palantir.Commands.Checks;

public class RequireServerHomeCheck(ServerHomeContext serverContext, Guilds.GuildsClient client) :
    IContextCheck<RequireServerHomeAttribute>,
    IContextCheck
{
    public async ValueTask<string?> ExecuteCheckAsync(
        RequireServerHomeAttribute attribute,
        CommandContext context)
    {
        GuildOptionsMessage server;

        var guildId = context.Guild?.Id;
        if (guildId is not { } guildIdValue)
            return "This command must be used in a server.";

        try
        {
            // attach server to context, if not already by parent check
            if (!serverContext.HasServerAssigned)
            {
                server = await client.GetGuildOptionsByIdAsync(new GetGuildOptionsByIdMessage
                    { GuildId = (long)guildIdValue });
                serverContext.Server = server;
            }
            else server = serverContext.Server;
        }
        catch
        {
            return "To use this command, you need to set up this server as typo server home. " +
                   $"You can read {"this article".AsTypoLink("https://www.typo.rip/help/lobby-bot", "📑")} to find out more.";
        }

        return null;
    }
}