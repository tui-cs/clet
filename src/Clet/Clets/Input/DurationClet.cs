using System.Globalization;
using System.Xml;
using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class DurationClet : ICliCommand<string?>
{
    public string PrimaryAlias => "duration";
    public IReadOnlyList<string> Aliases => ["duration"];
    public string Description => "Prompts for a duration and returns an ISO-8601 duration string (e.g. PT1H30M).";
    public CommandKind Kind => CommandKind.Input;
    public Type ResultType => typeof (string);

    public IReadOnlyList<CommandOptionDescriptor> Options => [];

    public bool TryValidateInitial (string initial, CommandRunOptions options)
    {
        try
        {
            XmlConvert.ToTimeSpan (initial);

            return true;
        }
        catch (FormatException)
        {
            return TimeSpan.TryParse (initial, CultureInfo.InvariantCulture, out _);
        }
    }

    public async Task<CommandResult<string?>> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken)
    {
        TimeEditor editor = new ();

        if (initial is not null)
        {
            try
            {
                var parsed = XmlConvert.ToTimeSpan (initial);
                editor.Value = parsed;
            }
            catch (FormatException)
            {
                if (TimeSpan.TryParse (initial, CultureInfo.InvariantCulture, out TimeSpan fallback))
                {
                    editor.Value = fallback;
                }
            }
        }

        RunnableWrapper<TimeEditor, TimeSpan?> wrapper = new (editor)
        {
            ResultExtractor = e => ((IValue<TimeSpan>)e).Value,
        };

        return await InputCletRunner.RunAsync<TimeEditor, TimeSpan?, string?> (
            app, wrapper, options,
            "Enter a duration (Enter to accept, Esc to cancel)",
            cancellationToken,
            result =>
            {
                string? formatted = result is { } ts ? XmlConvert.ToString (ts) : null;

                return new (CommandStatus.Ok, formatted, null, null);
            });
    }
}
