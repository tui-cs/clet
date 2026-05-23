using Terminal.Gui.App;

namespace Terminal.Gui.Cli;

/// <summary>
/// A CLI command backed by a Terminal.Gui View. Self-describes its alias,
/// options, and kind. Implemented by consumer apps.
/// </summary>
public interface ICliCommand
{
    string PrimaryAlias { get; }
    IReadOnlyList<string> Aliases { get; }
    string Description { get; }
    CommandKind Kind { get; }
    Type ResultType { get; }
    IReadOnlyList<CommandOptionDescriptor> Options { get; }

    /// <summary>Whether this command consumes positional arguments.</summary>
    bool AcceptsPositionalArgs => false;

    /// <summary>Validates the --initial value before the TUI starts.</summary>
    bool TryValidateInitial (string initial, CommandRunOptions options) => true;

    /// <summary>Non-generic dispatch entry point.</summary>
    Task<CommandResult> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken);
}
