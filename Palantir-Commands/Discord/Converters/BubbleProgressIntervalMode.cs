using DSharpPlus;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using tobeh.Valmar;

namespace Palantir_Commands.Discord.Converters;

public class BubbleProgressIntervalModeArgumentConverter : ITextArgumentConverter<BubbleProgressIntervalMode>, ISlashArgumentConverter<BubbleProgressIntervalMode>
{
    public Task<Optional<BubbleProgressIntervalMode>> ConvertAsync(TextConverterContext context,
        MessageCreateEventArgs eventArgs) => Parse(context.Argument);

    public Task<Optional<BubbleProgressIntervalMode>> ConvertAsync(InteractionConverterContext context,
        InteractionCreateEventArgs eventArgs) => Parse(context.Argument.RawValue);
    
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
    public ApplicationCommandOptionType ParameterType { get; init; }
}