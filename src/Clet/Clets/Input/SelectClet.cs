using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class SelectClet : ICliCommand<string?>
{
    public string PrimaryAlias => "select";
    public IReadOnlyList<string> Aliases => ["select"];
    public string Description => "Presents a list of options and returns the text of the selected item.";
    public CommandKind Kind => CommandKind.Input;
    public Type ResultType => typeof (string);

    public IReadOnlyList<CommandOptionDescriptor> Options =>
    [
        new ("options", "o", typeof (string), "Comma-separated list of options to display.", true, null),
    ];

    public bool AcceptsPositionalArgs => true;

    public async Task<CommandResult<string?>> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new (CommandStatus.Cancelled, default, null, null);
        }

        string[] labels = options.Arguments is { Count: > 0 }
            ? LabelParser.Split (options.Arguments)
            : options.CommandOptions.TryGetValue ("options", out string? optionsValue)
                ? LabelParser.Split (optionsValue)
                : [];

        OptionSelector selector = new ()
        {
            Labels = labels,
            AssignHotKeys = true,
        };

        if (initial is not null)
        {
            int initialIdx = Array.FindIndex (labels, l => string.Equals (l, initial, StringComparison.OrdinalIgnoreCase));

            if (initialIdx >= 0)
            {
                selector.Value = initialIdx;
            }
        }

        RunnableWrapper<OptionSelector, int?> wrapper = new (selector);

        return await InputCletRunner.RunAsync<OptionSelector, int?, string?> (
            app, wrapper, options,
            "Select an option (Enter to accept, Esc to cancel)",
            cancellationToken,
            result =>
            {
                string? selectedText = result is >= 0 and var idx && idx < labels.Length
                    ? labels[idx]
                    : null;

                return new (CommandStatus.Ok, selectedText, null, null);
            },
            addEnterBinding: false);
    }
}
