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

        // create text processor for prefix
        await commands.AddProcessorsAsync(new TextCommandProcessor
        {
            Configuration = new TextCommandConfiguration
            {
                PrefixResolver = new DefaultPrefixResolver(".").ResolvePrefixAsync
            }
        });
        await commands.AddProcessorAsync(new SlashCommandProcessor());
        
        // add custom checks
        commands.AddCheck<RequirePalantirMemberCheck>();
        
        // add command modules
        commands.AddCommands(typeof(DevelopmentCommands));
        commands.AddCommands(typeof(SpriteCommands));
        commands.AddCommands(typeof(SceneCommands));
        commands.AddCommands(typeof(LeagueCommands));
        
        await _client.ConnectAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping Discord Bot Client");
        await _client.DisconnectAsync();
    }
}