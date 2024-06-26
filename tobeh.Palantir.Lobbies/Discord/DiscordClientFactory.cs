using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.Extensions.Logging;
using tobeh.Palantir.Commands.Checks;
using tobeh.Palantir.Commands.Commands;
using tobeh.Palantir.Commands.Handlers;
using tobeh.Palantir.Commands.XmlDoc;

namespace tobeh.Palantir.Lobbies.Discord;

public class DiscordClientFactory(
    ILogger<DiscordClientFactory> logger,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider)
{
    public async Task<DiscordClient> CreateAndStartClientAsync(string discordToken, string prefix)
    {
        logger.LogTrace("CreateAndStartClientAsync(discordToken={discordToken}", discordToken);

        var client = new DiscordClient(new DiscordConfiguration
        {
            Token = discordToken,
            TokenType = TokenType.Bot,
            LoggerFactory = loggerFactory,
            Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents
        });

        // use interactivity
        client.UseInteractivity(new InteractivityConfiguration());

        // use help command documentation mapper
        client.GuildDownloadCompleted += HelpCommandDocumentationMapperEventHandlers.OnGuildDownloadCompleted;

        // use commands extension
        var commands = client.UseCommands(new CommandsConfiguration
        {
            ServiceProvider = serviceProvider,
            UseDefaultCommandErrorHandler = false,
            RegisterDefaultCommandProcessors = false
        });

        // use custom error handler
        commands.CommandErrored += CommandErroredHandler.OnCommandErrored;

        // create text processor
        var textCommandProcessor = new TextCommandProcessor
        {
            Configuration = new TextCommandConfiguration
            {
                PrefixResolver = new DefaultPrefixResolver(prefix).ResolvePrefixAsync
            }
        };
        await commands.AddProcessorsAsync(textCommandProcessor);

        // add custom checks
        commands.AddCheck<RequirePalantirMemberCheck>();
        commands.AddCheck<RequireServerHomeCheck>();

        // add command modules
        commands.AddCommands(typeof(ManagementCommands));
        commands.AddCommands(typeof(SpriteCommands));
        commands.AddCommands(typeof(SceneCommands));
        commands.AddCommands(typeof(LeagueCommands));
        commands.AddCommands(typeof(SplitCommands));
        commands.AddCommands(typeof(OutfitCommands));
        commands.AddCommands(typeof(AwardCommands));
        commands.AddCommands(typeof(EventCommands));
        commands.AddCommands(typeof(CalcCommands));
        commands.AddCommands(typeof(MiscCommands));
        commands.AddCommands(typeof(CardCommands));
        commands.AddCommands(typeof(PatronCommands));
        commands.AddCommands(typeof(HelpCommand));
        commands.AddCommands(typeof(ServerCommands));
        commands.AddCommands(typeof(WebhookCommands));

        await client.ConnectAsync();
        return client;
    }
}