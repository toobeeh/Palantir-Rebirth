using System.Text;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using tobeh.Palantir.Commands.Extensions;

namespace tobeh.Palantir.Commands.XmlDoc
{
    public static class HelpCommand
    {
        /// <summary>
        /// View a manual for a command, or view a list of all commands
        /// </summary>
        /// <param name="context"></param>
        /// <param name="command">A command name</param>
        /// <returns></returns>
        [Command("help"), TextAlias("hp", "manual", "man")]
        public static ValueTask ShowHelp(CommandContext context, [RemainingText] string? command = null)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return context.RespondAsync(GetHelpMessage(context));
            }
            else if (GetCommand(context.Extension.Commands.Values, command) is { } foundCommand)
            {
                return context.RespondAsync(GetHelpMessage(context, foundCommand));
            }

            return context.RespondAsync(new DiscordEmbedBuilder()
                .AddField("Command suggestions",
                    string.Join("\n",
                        context.Extension.FindSimilarCommands(command).Take(5)
                            .Select(c => $"- `/{c.Command}` {c.Description}")))
                .WithPalantirErrorPresets(context, $"Command `{command}` not found",
                    "The command you are looking for does not exist.\nUse `/help` to view a list of all commands."));
        }

        /// <summary>
        /// Search for similar commands
        /// </summary>
        /// <param name="context"></param>
        /// <param name="command">A command name</param>
        /// <returns></returns>
        [Command("find"), TextAlias("fd")]
        public static ValueTask FindCommands(CommandContext context, [RemainingText] string command)
        {
            return context.RespondAsync(new DiscordEmbedBuilder()
                .AddField($"Commands matching `/{command}`",
                    string.Join("\n",
                        context.Extension.FindSimilarCommands(command).Take(20)
                            .Select(c => $"- `/{c.Command}` {c.Description}")))
                .WithPalantirPresets(context)
                .WithTitle("Command search results"));
        }

        public static DiscordMessageBuilder GetHelpMessage(CommandContext context)
        {
            StringBuilder stringBuilder = new();
            foreach (Command command in context.Extension.Commands.Values.OrderBy(x => x.Name))
            {
                stringBuilder.AppendLine(
                    $"`{command.Name}`: {(HelpCommandDocumentationMapperEventHandlers.CommandDocumentation.TryGetValue(command, out string? documentation) ? documentation : "No description provided.")}");
            }

            return new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithTitle("Bot Commands")
                .WithDescription(
                    $"Use the command `/help (command)` to view a manual for the command.\n\n{stringBuilder}"));
        }

        public static DiscordMessageBuilder GetHelpMessage(CommandContext context, Command command)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle($"Command manual for `{command.Name}`")
                .WithPalantirPresets(context)
                .WithDescription(
                    HelpCommandDocumentationMapperEventHandlers.CommandDocumentation.TryGetValue(command,
                        out string? documentation)
                        ? documentation
                        : "No description provided.");

            var aliases = command.Attributes.Where(att => att is TextAliasAttribute).ToList();
            if (aliases.Count > 0)
            {
                embed.AddField("`⚡` Shortcut",
                    $"This command has shortcuts: `{string.Join(", ", aliases.Select(att => string.Join(", ", ((TextAliasAttribute)att).Aliases)))}`");
            }

            if (command.Subcommands.Count > 0)
            {
                embed.AddField("`📂` Command Group",
                    $"This is a command group with subcommands.\nUse `/help {command.FullName} (subcommand)` to view a manual for a subcommand.\n_ _\n_ _");
                embed.AddField("`📚` Subcommands", "_ _");
                foreach (var subcommand in command.Subcommands.OrderBy(x => x.Name))
                {
                    var isDefault = subcommand.Attributes.Any(att => att is DefaultGroupCommandAttribute);
                    embed.AddField($"{subcommand.Name} {(isDefault ? "`(default)`" : "")}",
                        HelpCommandDocumentationMapperEventHandlers.CommandDocumentation.GetValueOrDefault(subcommand,
                            "No description provided."));
                }
            }
            else
            {
                if (command.Attributes.FirstOrDefault(x => x is RequirePermissionsAttribute) is
                    RequirePermissionsAttribute permissions)
                {
                    var commonPermissions = permissions.BotPermissions & permissions.UserPermissions;
                    var botUniquePermissions = permissions.BotPermissions ^ commonPermissions;
                    var userUniquePermissions = permissions.UserPermissions ^ commonPermissions;
                    StringBuilder builder = new();
                    if (commonPermissions != default)
                    {
                        builder.AppendLine(commonPermissions.ToString("name"));
                    }

                    if (botUniquePermissions != default)
                    {
                        builder.Append("**Bot**: ");
                        builder.AppendLine((permissions.BotPermissions ^ commonPermissions).ToString("name"));
                    }

                    if (userUniquePermissions != default)
                    {
                        builder.Append("**User**: ");
                        builder.AppendLine(permissions.UserPermissions.ToString("name"));
                    }

                    embed.AddField("Required Permissions", builder.ToString());
                }

                embed.AddField("`💳` Usage", $"<> = required, [] = optional\n{command.GetUsage()}");
                foreach (var parameter in command.Parameters)
                {
                    var paramType = context.Extension.GetProcessor<TextCommandProcessor>()
                        .Converters[GetConverterFriendlyBaseType(parameter.Type)].GetType();
                    HelpCommandArgumentNameMapping.Names.TryGetValue(paramType, out var desc);

                    embed.AddField(
                        $"{parameter.Name} - {desc ?? "Unknown argument type"}",
                        HelpCommandDocumentationMapperEventHandlers.CommandParameterDocumentation.GetValueOrDefault(
                            parameter, "No description provided."));
                }
            }

            return new DiscordMessageBuilder().AddEmbed(embed);
        }

        private static Command? GetCommand(IEnumerable<Command> commands, string name)
        {
            int index = name.IndexOf(' ');
            string commandName = index == -1 ? name : name[..index];
            foreach (var command in commands)
            {
                if (command.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase))
                {
                    return index == -1 ? command : GetCommand(command.Subcommands, name[(index + 1)..]);
                }
            }

            // Search aliases
            foreach (Command command in commands)
            {
                foreach (Attribute attribute in command.Attributes)
                {
                    if (attribute is not TextAliasAttribute aliasAttribute)
                    {
                        continue;
                    }

                    if (aliasAttribute.Aliases.Any(alias =>
                            alias.Equals(commandName, StringComparison.OrdinalIgnoreCase)))
                    {
                        return index == -1 ? command : GetCommand(command.Subcommands, name[(index + 1)..]);
                    }
                }
            }

            return null;
        }

        private static string GetUsage(this Command command)
        {
            StringBuilder builder = new();
            builder.AppendLine("```ansi");
            builder.Append('/');
            builder.Append(Formatter.Colorize(command.FullName, AnsiColor.Cyan));
            foreach (CommandParameter parameter in command.Parameters)
            {
                if (!parameter.DefaultValue.HasValue)
                {
                    builder.Append(Formatter.Colorize(" <", AnsiColor.LightGray));
                    builder.Append(Formatter.Colorize(parameter.Name, AnsiColor.Magenta));
                    builder.Append(Formatter.Colorize(">", AnsiColor.LightGray));
                }
                else if (parameter.DefaultValue.Value !=
                         (parameter.Type.IsValueType ? Activator.CreateInstance(parameter.Type) : null))
                {
                    builder.Append(Formatter.Colorize(" [", AnsiColor.Yellow));
                    builder.Append(Formatter.Colorize(parameter.Name, AnsiColor.Magenta));
                    builder.Append(Formatter.Colorize($" = ", AnsiColor.LightGray));
                    builder.Append(Formatter.Colorize($"\"{parameter.DefaultValue.Value}\"", AnsiColor.Cyan));
                    builder.Append(Formatter.Colorize("]", AnsiColor.Yellow));
                }
                else
                {
                    builder.Append(Formatter.Colorize(" [", AnsiColor.Yellow));
                    builder.Append(Formatter.Colorize(parameter.Name, AnsiColor.Magenta));
                    builder.Append(Formatter.Colorize("]", AnsiColor.Yellow));
                }
            }

            builder.Append("```");
            return builder.ToString();
        }

        private static Type GetConverterFriendlyBaseType(Type type)
        {
            ArgumentNullException.ThrowIfNull(type, nameof(type));

            if (type.IsEnum)
            {
                return typeof(Enum);
            }
            else if (type.IsArray)
            {
                return type.GetElementType()!;
            }

            return Nullable.GetUnderlyingType(type) ?? type;
        }
    }
}