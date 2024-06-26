using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using tobeh.Palantir.Commands.Checks;
using tobeh.Palantir.Commands.Commands;
using tobeh.Palantir.Commands.Handlers;
using tobeh.Palantir.Commands.XmlDoc;

namespace tobeh.Palantir.Public.Discord;

public class DiscordBotClient(
    ILogger<DiscordBotClient> logger,
    IOptions<DiscordBotClientOptions> options,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : IHostedService
{
    private readonly DiscordClient _client = new(new DiscordConfiguration
    {
        Token = options.Value.DiscordToken,
        TokenType = TokenType.Bot,
        LoggerFactory = loggerFactory,
        Intents = DiscordIntents.AllUnprivileged
    });

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting Discord Bot Client");

        // use interactivity
        _client.UseInteractivity(new InteractivityConfiguration());

        // use help command documentation mapper
        _client.GuildDownloadCompleted += HelpCommandDocumentationMapperEventHandlers.OnGuildDownloadCompleted;

        // use commands extension
        var commands = _client.UseCommands(new CommandsConfiguration
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
                PrefixResolver = new DefaultPrefixResolver(options.Value.Prefix).ResolvePrefixAsync
            }
        };
        await commands.AddProcessorsAsync(textCommandProcessor);

        // create slash processor, if configured
        if (options.Value.UseSlash)
        {
            var slashCommandProcessor = new SlashCommandProcessor();
            await commands.AddProcessorAsync(slashCommandProcessor);
        }

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

        await _client.ConnectAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping Discord Bot Client");
        await _client.DisconnectAsync();
    }
}