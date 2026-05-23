using System.Globalization;
using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class IntClet : ICliCommand<int?>
{
    public string PrimaryAlias => "int";
    public IReadOnlyList<string> Aliases => ["int"];
    public string Description => "Prompts for an integer value using a numeric spinner.";
    public CommandKind Kind => CommandKind.Input;
    public Type ResultType => typeof (int);

    public IReadOnlyList<CommandOptionDescriptor> Options =>
    [
        new ("step", null, typeof (int), "Step increment.", false, "1"),
    ];

    public bool TryValidateInitial (string initial, CommandRunOptions options)
        => int.TryParse (initial, CultureInfo.InvariantCulture, out _);

    public async Task<CommandResult<int?>> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken)
    {
        NumericUpDown<int> spinner = new ();

        if (options.CommandOptions.TryGetValue ("step", out string? stepStr)
            && int.TryParse (stepStr, CultureInfo.InvariantCulture, out int step))
        {
            spinner.Increment = step;
        }

        if (initial is not null
            && int.TryParse (initial, CultureInfo.InvariantCulture, out int initialValue))
        {
            spinner.Value = initialValue;
        }

        RunnableWrapper<NumericUpDown<int>, int?> wrapper = new (spinner)
        {
            ResultExtractor = s => s.Value,
        };

        return await InputCletRunner.RunAsync (
            app, wrapper, options,
            "Enter a number (Enter to accept, Esc to cancel)",
            cancellationToken);
    }
}
