using System.Text;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.EventArgs;
using DSharpPlus.Commands.Exceptions;
using DSharpPlus.Entities;
using Grpc.Core;
using tobeh.Palantir.Commands.Extensions;

namespace tobeh.Palantir.Commands.Handlers;

public static class CommandErroredHandler
{
    public static async Task OnCommandErrored(CommandsExtension extension, CommandErroredEventArgs args)
    {
        var embedBuilder = new DiscordEmbedBuilder()
            .WithPalantirErrorPresets(args.Context);

        switch (args.Exception)
        {
            case CommandNotFoundException cnfe:
                embedBuilder.WithTitle($"Command `{cnfe.CommandName}` not found");
                embedBuilder.WithDescription("Use the command `/help` to see a list of supported commands.");
                embedBuilder.AddField("Command suggestions",
                    string.Join("\n",
                        extension.FindSimilarCommands(cnfe.CommandName).Take(5).Select(c => $"- `/{c}`")));
                break;
            case CommandNotExecutableException cnee:
                embedBuilder.WithTitle($"Command `{cnee.Command.FullName}` not found");
                embedBuilder.WithDescription("Use the command `/help` to see a list of supported commands.");
                embedBuilder.AddField("Command suggestions",
                    string.Join("\n",
                        extension.FindSimilarCommands(cnee.Command.FullName).Take(5).Select(c => $"- `/{c}`")));
                break;

            case RpcException re:
                embedBuilder.WithTitle($"Something broke during command execution:");
                embedBuilder.WithDescription(
                    $"{re.Status.Detail}\n\nPlease try the command again; or ask for help on the Typo Discord server if the error persists.");
                break;

            case ChecksFailedException cfe:
                if (cfe.Errors.Any(err => err.ErrorMessage.Contains("Patronizer")))
                {
                    embedBuilder.WithTitle($"Uh oh, a wild paywall appeared.");
                    embedBuilder.WithDescription(
                        $"You need to be a {"Patronizer".AsTypoLink("https://www.patreon.com/skribbltypo", "🩵")} to use this command.\n");
                }
                else if (cfe.Errors.Any(err => err.ErrorMessage.Contains("Patron")))
                {
                    embedBuilder.WithTitle($"Uh oh, a wild paywall appeared.");
                    embedBuilder.WithDescription(
                        $"You need to be a {"Patron".AsTypoLink("https://www.patreon.com/skribbltypo", "🩵")} to use this command.\n");
                }
                else if (cfe.Errors.Any(err => err.ErrorMessage.Contains("permission")) &&
                         cfe.Command.Attributes.FirstOrDefault(x => x is RequirePermissionsAttribute) is
                             RequirePermissionsAttribute permissions)
                {
                    var commonPermissions = permissions.BotPermissions & permissions.UserPermissions;
                    var botUniquePermissions = permissions.BotPermissions ^ commonPermissions;
                    var userUniquePermissions = permissions.UserPermissions ^ commonPermissions;
                    StringBuilder builder = new();
                    if (commonPermissions != default)
                    {
                        builder.AppendLine(commonPermissions.ToPermissionString());
                    }

                    if (botUniquePermissions != default)
                    {
                        builder.Append("**Bot:** ");
                        builder.AppendLine((permissions.BotPermissions ^ commonPermissions).ToPermissionString());
                    }

                    if (userUniquePermissions != default)
                    {
                        builder.Append("**User:** ");
                        builder.AppendLine(permissions.UserPermissions.ToPermissionString());
                    }

                    embedBuilder.WithTitle($"You or the bot don't have the required permissions.");
                    embedBuilder.WithDescription(
                        $"Following permissions are required to use this command.\n{builder}");
                }
                else
                {
                    embedBuilder.WithTitle("You are not allowed to use this command:");
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