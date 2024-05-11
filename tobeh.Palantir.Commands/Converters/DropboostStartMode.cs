using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace tobeh.Palantir.Commands.Converters;

public enum DropboostStartMode
{
    Now,
    Check
}

public class DropboostStartModeArgumentConverter : ITextArgumentConverter<DropboostStartMode>,
    ISlashArgumentConverter<DropboostStartMode>
{
    public Task<Optional<DropboostStartMode>> ConvertAsync(TextConverterContext context,
        MessageCreateEventArgs eventArgs) => Parse(context.Argument);

    public Task<Optional<DropboostStartMode>> ConvertAsync(InteractionConverterContext context,
        InteractionCreateEventArgs eventArgs) => Parse(context.Argument.RawValue);

    private Task<Optional<DropboostStartMode>> Parse(string input)
    {
        return input switch
        {
            "now" => Task.FromResult(Optional.FromValue(DropboostStartMode.Now)),
            _ => Task.FromResult(Optional.FromValue(DropboostStartMode.Check))
        };
    }

    public bool RequiresText { get; init; }
    public DiscordApplicationCommandOptionType ParameterType { get; init; }
    public string ReadableName => "Dropboost Start Mode";
}