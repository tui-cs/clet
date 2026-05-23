using Terminal.Gui.App;

namespace Terminal.Gui.Cli;

/// <summary>Typed command that returns a value.</summary>
public interface ICliCommand<TValue> : ICliCommand
{
    new Task<CommandResult<TValue>> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken);

    // Default interface method bridges typed → untyped
    async Task<CommandResult> ICliCommand.RunAsync (
        IApplication app, string? initial,
        CommandRunOptions options, CancellationToken ct)
    {
        CommandResult<TValue> r = await RunAsync (app, initial, options, ct);

        return new (r.Status, r.Value, r.ErrorCode, r.ErrorMessage);
    }
}
