using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using tobeh.Valmar;

namespace tobeh.Palantir.Commands.Converters;

public class BubbleProgressIntervalModeArgumentConverter : ITextArgumentConverter<BubbleProgressIntervalMode>,
    ISlashArgumentConverter<BubbleProgressIntervalMode>
{
    public Task<Optional<BubbleProgressIntervalMode>> ConvertAsync(TextConverterContext context,
        MessageCreatedEventArgs eventArgs) => Parse(context.Argument);

    public Task<Optional<BubbleProgressIntervalMode>> ConvertAsync(InteractionConverterContext context,
        InteractionCreatedEventArgs eventArgs) => Parse(context.Argument.RawValue);

    private Task<Optional<BubbleProgressIntervalMode>> Parse(string input)
    {
        return input switch
        {
            "day" => Task.FromResult(Optional.FromValue(BubbleProgressIntervalMode.Day)),
            "week" => Task.FromResult(Optional.FromValue(BubbleProgressIntervalMode.Week)),
            "month" => Task.FromResult(Optional.FromValue(BubbleProgressIntervalMode.Month)),
            _ => Task.FromResult(Optional.FromNoValue<BubbleProgressIntervalMode>())
        };
    }

    public bool RequiresText { get; init; }
    public DiscordApplicationCommandOptionType ParameterType { get; init; }
    public string ReadableName => "Bubble Progress Interval";
}