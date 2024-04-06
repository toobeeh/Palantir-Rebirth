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
using Palantir_Commands.Discord.Checks;
using Palantir_Commands.Discord.Commands;
using Palantir_Commands.Discord.Converters;

namespace Palantir_Commands.Discord;

public class DiscordBotClient(ILogger<DiscordBotClient> logger, IOptions<DiscordBotClientOptions> options, ILoggerFactory loggerFactory, IServiceProvider serviceProvider) : IHostedService
{
    private readonly DiscordClient _client = new(new DiscordConfiguration
    {
        Token = options.Value.DiscordToken,
        TokenType = TokenType.Bot,
        LoggerFactory = loggerFactory,
        Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents
    });
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting Discord Bot Client");
        
        // use interactivity
        _client.UseInteractivity(new InteractivityConfiguration {});

        // use commands extension
        var commands = _client.UseCommands(new CommandsConfiguration
        {
            ServiceProvider = serviceProvider
        });
        
        // create argument converters
        var dropboostStartModeArgumentConverter = new DropboostStartModeArgumentConverter
        {
            ParameterType = ApplicationCommandOptionType.String
        };

        // create text processor
        var textCommandProcessor = new TextCommandProcessor
        {
            Configuration = new TextCommandConfiguration
            {
                PrefixResolver = new DefaultPrefixResolver(".").ResolvePrefixAsync
            }
        };
        textCommandProcessor.AddConverter<DropboostStartModeArgumentConverter>(dropboostStartModeArgumentConverter);
        await commands.AddProcessorsAsync(textCommandProcessor);
        
        // create slash processor
        var slashCommandProcessor = new SlashCommandProcessor();
        slashCommandProcessor.AddConverter<DropboostStartModeArgumentConverter>(dropboostStartModeArgumentConverter);
        await commands.AddProcessorAsync(slashCommandProcessor);
        
        // add custom checks
        commands.AddCheck<RequirePalantirMemberCheck>();
        
        // add command modules
        commands.AddCommands(typeof(DevelopmentCommands));
        commands.AddCommands(typeof(SpriteCommands));
        commands.AddCommands(typeof(SceneCommands));
        commands.AddCommands(typeof(LeagueCommands));
        commands.AddCommands([typeof(SplitCommands)]); // TODO remove enumerable workaround as soon as dsharpplus fixed
        
        await _client.ConnectAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping Discord Bot Client");
        await _client.DisconnectAsync();
    }
}