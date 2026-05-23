using System.Globalization;
using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class TimeClet : ICliCommand<string?>
{
    public string PrimaryAlias => "time";
    public IReadOnlyList<string> Aliases => ["time"];
    public string Description => "Prompts for a time and returns an ISO-8601 time string (HH:MM:SS).";
    public CommandKind Kind => CommandKind.Input;
    public Type ResultType => typeof (string);

    public IReadOnlyList<CommandOptionDescriptor> Options => [];

    public bool TryValidateInitial (string initial, CommandRunOptions options)
        => TimeSpan.TryParse (initial, CultureInfo.InvariantCulture, out _);

    public async Task<CommandResult<string?>> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken)
    {
        TimeEditor editor = new ();

        if (initial is not null
            && TimeSpan.TryParse (initial, CultureInfo.InvariantCulture, out TimeSpan initialTime))
        {
            editor.Value = initialTime;
        }

        RunnableWrapper<TimeEditor, TimeSpan?> wrapper = new (editor)
        {
            ResultExtractor = e => ((IValue<TimeSpan>)e).Value,
        };

        return await InputCletRunner.RunAsync<TimeEditor, TimeSpan?, string?> (
            app, wrapper, options,
            "Select a time (Enter to accept, Esc to cancel)",
            cancellationToken,
            result =>
            {
                string? formatted = result?.ToString (@"hh\:mm\:ss", CultureInfo.InvariantCulture);

                return new (CommandStatus.Ok, formatted, null, null);
            });
    }
}
