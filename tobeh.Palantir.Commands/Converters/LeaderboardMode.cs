using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using tobeh.Valmar;

namespace tobeh.Palantir.Commands.Converters;

public class LeaderboardModeArgumentConverter : ITextArgumentConverter<LeaderboardMode>,
    ISlashArgumentConverter<LeaderboardMode>
{
    public Task<Optional<LeaderboardMode>> ConvertAsync(TextConverterContext context,
        MessageCreatedEventArgs eventArgs) => Parse(context.Argument);

    public Task<Optional<LeaderboardMode>> ConvertAsync(InteractionConverterContext context,
        InteractionCreatedEventArgs eventArgs) => Parse(context.Argument.RawValue);

    private Task<Optional<LeaderboardMode>> Parse(string input)
    {
        return input switch
        {
            "drops" => Task.FromResult(Optional.FromValue(LeaderboardMode.Drops)),
            "bubbles" => Task.FromResult(Optional.FromValue(LeaderboardMode.Bubbles)),
            _ => Task.FromResult(Optional.FromNoValue<LeaderboardMode>())
        };
    }

    public bool RequiresText { get; init; }
    public DiscordApplicationCommandOptionType ParameterType { get; init; }
    public string ReadableName => "Leaderboard Order";
}