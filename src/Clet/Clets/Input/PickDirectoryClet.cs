using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class PickDirectoryClet : ICliCommand<string?>
{
    public string PrimaryAlias => "pick-directory";
    public IReadOnlyList<string> Aliases => ["pick-directory", "dir"];
    public string Description => "Opens a directory picker dialog and returns the selected directory path.";
    public CommandKind Kind => CommandKind.Input;
    public Type ResultType => typeof (string);

    public IReadOnlyList<CommandOptionDescriptor> Options =>
    [
        new ("root", "r", typeof (string), "Starting directory (not a sandbox — user can navigate freely).", false, null),
    ];

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

        string? root = options.CommandOptions.TryGetValue ("root", out string? rootStr) ? rootStr : null;
        string? startPath = root ?? initial;

        OpenDialog dialog = new ()
        {
            Title = options.Title ?? "Select a directory (Enter to accept, Esc to cancel)",
            Width = Dim.Fill (),
            Height = 25,
            OpenMode = OpenMode.Directory,
            BorderStyle = LineStyle.Rounded,
            ShadowStyle = null,
            SchemeName = CletStyling.BaseSchemeName,
        };
        dialog.Border.Thickness = new Thickness (0, 1, 0, 0);

        dialog.IsRunningChanged += (_, _) =>
        {
            if (dialog.IsRunning)
            {
                dialog.SchemeName = CletStyling.BaseSchemeName;
            }
        };

        if (startPath is not null)
        {
            dialog.Path = startPath;
        }

        try
        {
            await app.RunAsync (dialog, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new (CommandStatus.Cancelled, default, null, null);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new (CommandStatus.Cancelled, default, null, null);
        }

        IReadOnlyList<string> paths = dialog.FilePaths;

        if (paths.Count == 0)
        {
            return new (CommandStatus.Cancelled, default, null, null);
        }

        return new (CommandStatus.Ok, paths[0], null, null);
    }
}
