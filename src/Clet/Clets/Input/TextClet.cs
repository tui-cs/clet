using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.Document;
using Terminal.Gui.Drawing;
using Terminal.Gui.Editor;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class TextClet : ICliCommand<string?>
{
    public string PrimaryAlias => "text";
    public IReadOnlyList<string> Aliases => ["text", "multiline-text", "mt"];
    public string Description => "Prompts for multi-line text input using an editor and returns the entered string.";
    public CommandKind Kind => CommandKind.Input;
    public Type ResultType => typeof (string);

    public IReadOnlyList<CommandOptionDescriptor> Options => [];

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

        int rows = options.Rows ?? 5;

        Editor editor = new ()
        {
            Document = new TextDocument (initial ?? string.Empty),
            Width = Dim.Fill (),
            Height = rows,
            ConvertTabsToSpaces = true,
        };

        Button okButton = new ()
        {
            Text = Terminal.Gui.Resources.Strings.btnOk,
            Y = Pos.Bottom (editor),
        };

        RunnableWrapper<Editor, string?> wrapper = new (editor)
        {
            Title = options.Title ?? "Enter text (OK to accept, Esc to cancel)",
            Width = Dim.Fill (),
            BorderStyle = LineStyle.Rounded,
            ResultExtractor = e => e.Document?.Text,
            SchemeName = CletStyling.BaseSchemeName,
        };
        wrapper.Border.Thickness = new Thickness (0, 1, 0, 0);
        wrapper.Add (okButton);

        okButton.Accepted += (_, _) => wrapper.InvokeCommand (Command.Accept);

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

        string? result = wrapper.Result;

        return new (CommandStatus.Ok, result, null, null);
    }
}
