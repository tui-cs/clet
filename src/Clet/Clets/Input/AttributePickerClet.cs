using System.Text.Json.Nodes;
using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TgAttribute = Terminal.Gui.Drawing.Attribute;

namespace Clet;

internal sealed class AttributePickerClet : ICliCommand<JsonObject?>
{
    public string PrimaryAlias => "attribute-picker";
    public IReadOnlyList<string> Aliases => ["attribute-picker", "attribute"];
    public string Description => "Prompts for text attributes (foreground, background, style) and returns a JSON object.";
    public CommandKind Kind => CommandKind.Input;
    public Type ResultType => typeof (JsonObject);

    public IReadOnlyList<CommandOptionDescriptor> Options => [];

    public async Task<CommandResult<JsonObject?>> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new (CommandStatus.Cancelled, default, null, null);
        }

        AttributePicker picker = new ();

        foreach (View sub in picker.SubViews)
        {
            if (sub is ColorPicker cp)
            {
                cp.Style.ShowColorName = true;
                cp.ApplyStyleChanges ();
            }
        }

        RunnableWrapper<AttributePicker, TgAttribute?> wrapper = new (picker)
        {
            Title = options.Title ?? "Pick text attributes (Enter to accept, Esc to cancel)",
            Width = Dim.Fill (),
            BorderStyle = LineStyle.Rounded,
            SchemeName = CletStyling.BaseSchemeName,
        };
        wrapper.Border.Thickness = new Thickness (0, 1, 0, 0);

        try
        {
            await app.RunAsync (wrapper, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new (CommandStatus.Cancelled, default, null, null);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new (CommandStatus.Cancelled, default, null, null);
        }

        TgAttribute? result = wrapper.Result;

        if (result is not { } attr)
        {
            return new (CommandStatus.Ok, null, null, null);
        }

        Color fg = attr.Foreground;
        Color bg = attr.Background;

        JsonObject obj = new ()
        {
            ["fg"] = $"#{fg.R:x2}{fg.G:x2}{fg.B:x2}",
            ["bg"] = $"#{bg.R:x2}{bg.G:x2}{bg.B:x2}",
            ["style"] = attr.Style.ToString ().ToLowerInvariant (),
        };

        return new (CommandStatus.Ok, obj, null, null);
    }
}
