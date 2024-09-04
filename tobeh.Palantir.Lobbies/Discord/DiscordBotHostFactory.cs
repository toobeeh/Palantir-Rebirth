using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using tobeh.Palantir.Commands;
using tobeh.Palantir.Commands.Checks;
using tobeh.Palantir.Commands.Commands;
using tobeh.Palantir.Commands.Handlers;
using tobeh.Palantir.Commands.XmlDoc;
using tobeh.TypoContentService.Client.Util;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Lobbies.Discord;

public class DiscordBotHostFactory(
    ILogger<DiscordBotHostFactory> logger,
    IConfiguration configuration)
{
    public IHost CreateBotHost(string discordToken, string prefix,
        Action<EventHandlingBuilder> discordEventhandlerBuilder)
    {
        logger.LogTrace("CreateAndStartClientAsync(discordToken={discordToken}, prefix={prefix})", discordToken,
            prefix);

        var builder = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddDiscordClient(discordToken,
                        DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents)
                    .AddCommandsExtension(extension =>
                    {
                        // add commands
                        extension.AddCommands([
                            typeof(ManagementCommands),
                            typeof(SpriteCommands),
                            typeof(SceneCommands),
                            typeof(LeagueCommands),
                            typeof(SplitCommands),
                            typeof(OutfitCommands),
                            typeof(AwardCommands),
                            typeof(EventCommands),
                            typeof(CalcCommands),
                            typeof(MiscCommands),
                            typeof(CardCommands),
                            typeof(PatronCommands),
                            typeof(HelpCommand),
                            typeof(ServerCommands),
                            typeof(WebhookCommands)
                        ]);

                        // add prefix resolver
                        var textCommandProcessor = new TextCommandProcessor
                        {
                            Configuration = new TextCommandConfiguration
                            {
                                PrefixResolver = new DefaultPrefixResolver(true, prefix).ResolvePrefixAsync
                            }
                        };
                        extension.AddProcessors(textCommandProcessor);

                        // add command error handler
                        extension.CommandErrored += CommandErroredHandler.OnCommandErrored;

                        // add checks
                        extension.AddCheck<RequirePalantirMemberCheck>();
                        extension.AddCheck<RequireServerHomeCheck>();
                    }, new CommandsConfiguration
                    {
                        UseDefaultCommandErrorHandler = false,
                        RegisterDefaultCommandProcessors = false
                    })
                    .ConfigureEventHandlers(discordEventhandlerBuilder.Invoke)
                    .AddHostedService<DiscordHostedBot>(s => s.GetRequiredService<DiscordHostedBot>())
                    .AddSingleton<DiscordHostedBot>()
                    .AddScoped<MemberContext>()
                    .AddScoped<ServerHomeContext>()
                    .AddLogging(loggingBuilder => loggingBuilder
                        .AddConfiguration(configuration.GetSection("Logging"))
                        .AddConsole())
                    .AddTypoContentServiceGrpc(configuration.GetValue<string>("Grpc:ContentServiceAddress"))
                    .AddValmarGrpc(configuration.GetValue<string>("Grpc:ValmarAddress"))
                    .BuildServiceProvider();
            })
            .Build();

        return builder;
    }
}