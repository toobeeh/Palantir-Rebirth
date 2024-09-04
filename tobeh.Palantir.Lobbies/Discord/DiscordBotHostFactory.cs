using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using tobeh.Palantir.Commands;
using tobeh.Palantir.Commands.Commands;
using tobeh.Palantir.Commands.Handlers;
using tobeh.Palantir.Commands.XmlDoc;

namespace tobeh.Palantir.Lobbies.Discord;

public class DiscordBotHostFactory(
    ILogger<DiscordBotHostFactory> logger,
    IServiceProvider serviceProvider)
{
    public IHost CreateBotHost(string discordToken, string prefix,
        Action<EventHandlingBuilder> discordEventhandlerBuilder)
    {
        logger.LogTrace("CreateAndStartClientAsync(discordToken={discordToken}, prefix={prefix})", discordToken,
            prefix);

        var builder = new HostBuilder()
            .ConfigureServices(services =>
            {
                // Register existing services from the serviceProvider
                foreach (var serviceDescriptor in serviceProvider.GetService<IEnumerable<ServiceDescriptor>>() ?? [])
                {
                    services.Add(serviceDescriptor);
                }

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
                        extension.CommandErrored += CommandErroredHandler.OnCommandErrored;
                    }, new CommandsConfiguration
                    {
                        UseDefaultCommandErrorHandler = false,
                        RegisterDefaultCommandProcessors = true
                    })
                    .ConfigureEventHandlers(discordEventhandlerBuilder.Invoke)
                    .AddSingleton<DiscordHostedBot>();
            })
            .Build();

        return builder;
    }
}