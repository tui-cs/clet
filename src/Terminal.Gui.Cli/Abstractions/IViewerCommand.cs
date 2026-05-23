namespace Terminal.Gui.Cli;

/// <summary>
/// Viewer command. Does not return a typed value in the JSON envelope (status-only).
/// Viewers range from simple read-only content display (help, markdown browser) to
/// heavyweight stateful tools (file editor with undo/redo, config manager with
/// validation and save). They are full TUI applications; "viewer" means "no typed
/// result," not "no interactivity."
/// </summary>
public interface IViewerCommand : ICliCommand
{
    /// <summary>
    /// Renders content to stdout without launching the TUI. Called when --cat is set.
    /// Return null to indicate --cat is not supported (dispatcher falls through to
    /// normal TUI dispatch). The library skips Application.Create() when this returns
    /// a non-null result.
    /// </summary>
    Task<CommandResult?> RenderCatAsync (
        CommandRunOptions options,
        TextWriter stdout,
        CancellationToken cancellationToken) => Task.FromResult<CommandResult?> (null);
}
