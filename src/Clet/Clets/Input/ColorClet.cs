using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.Drawing;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class ColorClet : ICliCommand<string?>
{
    public string PrimaryAlias => "color";
    public IReadOnlyList<string> Aliases => ["color"];
    public string Description => "Prompts for a color and returns a hex string (#rrggbb).";
    public CommandKind Kind => CommandKind.Input;
    public Type ResultType => typeof (string);

    public IReadOnlyList<CommandOptionDescriptor> Options => [];

    public bool TryValidateInitial (string initial, CommandRunOptions options)
        => Color.TryParse (initial, null, out _);

    public async Task<CommandResult<string?>> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken)
    {
        ColorPicker picker = new ();
        picker.Style.ShowColorName = true;
        picker.ApplyStyleChanges ();

        if (initial is not null && Color.TryParse (initial, null, out Color parsed))
        {
            picker.SelectedColor = parsed;
        }

        RunnableWrapper<ColorPicker, Color?> wrapper = new (picker);

        return await InputCletRunner.RunAsync<ColorPicker, Color?, string?> (
            app, wrapper, options,
            "Pick a color (Enter to accept, Esc to cancel)",
            cancellationToken,
            result =>
            {
                string? hex = result is { } c ? $"#{c.R:x2}{c.G:x2}{c.B:x2}" : null;

                return new (CommandStatus.Ok, hex, null, null);
            },
            addEnterBinding: false);
    }
}
