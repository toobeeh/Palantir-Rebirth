using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.EventArgs;
using DSharpPlus.Commands.Exceptions;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Palantir_Commands.Discord.Checks;
using Palantir_Commands.Discord.Commands;
using Palantir_Commands.Discord.Extensions;
using Palantir_Commands.Discord.XmlDoc;

namespace Palantir_Commands.Discord;

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
        Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents
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
            UseDefaultCommandErrorHandler = false
        });

        // use custom error handler
        commands.CommandErrored += HandleError;

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

        // add command modules
        commands.AddCommands(typeof(DevelopmentCommands));
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

        await _client.ConnectAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping Discord Bot Client");
        await _client.DisconnectAsync();
    }

    private async Task HandleError(CommandsExtension extension, CommandErroredEventArgs args)
    {
        logger.LogWarning("HandleError({args})", args.Exception);

        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirErrorPresets(args.Context);

        switch (args.Exception)
        {
            case CommandNotFoundException cnfe:
            case CommandNotExecutableException cnee:
                embedBuilder.WithTitle($"Command `{args.Context.Command.FullName}` not found");
                embedBuilder.WithDescription("Use the command `/help` to see a list of supported commands.");
                break;

            case RpcException re:
                embedBuilder.WithTitle($"Something broke during command execution:");
                embedBuilder.WithDescription(
                    $"{re.Status.Detail}\n\nPlease try the command again; or ask for help on the Typo Discord server if the error persists.");
                break;

            case ChecksFailedException cfe:
                if (cfe.Errors.Any(err => err.ErrorMessage.Contains("Patron")))
                {
                    embedBuilder.WithTitle($"Uh oh, a wild paywall appeared.");
                    embedBuilder.WithDescription(
                        $"You need to be a {"Patron".AsTypoLink("https://www.patreon.com/skribbltypo", "🩵")} to use this command.\n");
                }
                else
                {
                    embedBuilder.WithTitle("You are not allowed to use this command :");
                    embedBuilder.WithDescription(string.Join("\n", cfe.Errors.Select(e => $"{e.ErrorMessage}")));
                }

                break;

            case ArgumentParseException ape:
                embedBuilder.WithTitle($"Incorrect command usage :(");
                embedBuilder.WithDescription(
                    $"The value for the parameter `{ape.Parameter.Name}` is invalid.\n Use the command `/help {args.Context.Command.FullName}` for more information.");
                break;

            default:
                embedBuilder.WithTitle($"An unexpected error occurred:");
                embedBuilder.WithDescription(args.Exception.Message);
                break;
        }

        await args.Context.RespondAsync(embedBuilder.Build());
    }
}