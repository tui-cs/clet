using System.Globalization;
using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class DecimalClet : ICliCommand<decimal?>
{
    public string PrimaryAlias => "decimal";
    public IReadOnlyList<string> Aliases => ["decimal"];
    public string Description => "Prompts for a decimal value using a numeric spinner.";
    public CommandKind Kind => CommandKind.Input;
    public Type ResultType => typeof (decimal);

    public IReadOnlyList<CommandOptionDescriptor> Options =>
    [
        new ("step", null, typeof (decimal), "Step increment.", false, "0.1"),
    ];

    public bool TryValidateInitial (string initial, CommandRunOptions options)
        => decimal.TryParse (initial, CultureInfo.InvariantCulture, out _);

    public async Task<CommandResult<decimal?>> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken)
    {
        NumericUpDown<decimal> spinner = new ()
        {
            Increment = 0.1m,
            Format = "{0:0.###}",
        };

        if (options.CommandOptions.TryGetValue ("step", out string? stepStr)
            && decimal.TryParse (stepStr, CultureInfo.InvariantCulture, out decimal step))
        {
            spinner.Increment = step;
        }

        if (initial is not null
            && decimal.TryParse (initial, CultureInfo.InvariantCulture, out decimal initialValue))
        {
            spinner.Value = initialValue;
        }

        RunnableWrapper<NumericUpDown<decimal>, decimal?> wrapper = new (spinner)
        {
            ResultExtractor = s => s.Value,
        };

        return await InputCletRunner.RunAsync (
            app, wrapper, options,
            "Enter a decimal (Enter to accept, Esc to cancel)",
            cancellationToken);
    }
}
