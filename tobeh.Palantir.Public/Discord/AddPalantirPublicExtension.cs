using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using tobeh.Palantir.Commands;
using tobeh.Palantir.Commands.Commands;
using tobeh.Palantir.Commands.Handlers;
using tobeh.Palantir.Commands.XmlDoc;

namespace tobeh.Palantir.Public.Discord;

public static class AddPalantirPublicExtension
{
    public static IServiceCollection AddPalantirPublic(this IServiceCollection services,
        DiscordBotClientOptions options)
    {
        services.AddDiscordClient(options.DiscordToken,
                DiscordIntents.AllUnprivileged)
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
                        PrefixResolver = new DefaultPrefixResolver(true, options.Prefix).ResolvePrefixAsync
                    }
                };
                extension.AddProcessor(textCommandProcessor);

                // add slash commands
                if (options.UseSlash)
                {
                    var slashCommandProcessor = new SlashCommandProcessor();
                    extension.AddProcessor(slashCommandProcessor);
                }

                // add command error handler
                extension.CommandErrored += CommandErroredHandler.OnCommandErrored;
            }, new CommandsConfiguration
            {
                UseDefaultCommandErrorHandler = false,
                RegisterDefaultCommandProcessors = false
            })
            .ConfigureEventHandlers(builder =>
            {
                builder.HandleGuildDownloadCompleted(HelpCommandDocumentationMapperEventHandlers
                    .OnGuildDownloadCompleted);
            })
            .AddHostedService<DiscordHostedBot>();

        return services;
    }
}