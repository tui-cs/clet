namespace Terminal.Gui.Cli;

/// <summary>Describes a consumer-defined global option.</summary>
public sealed record GlobalOptionDescriptor (
    string Name,
    string? ShortName,
    string Description,
    bool IsFlag,
    bool Repeatable = false);
