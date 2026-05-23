namespace Terminal.Gui.Cli;

/// <summary>Non-generic result for dispatch and output formatting.</summary>
public readonly record struct CommandResult (
    CommandStatus Status,
    object? Value,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>Typed result returned by input commands.</summary>
public readonly record struct CommandResult<T> (
    CommandStatus Status,
    T? Value,
    string? ErrorCode,
    string? ErrorMessage);
