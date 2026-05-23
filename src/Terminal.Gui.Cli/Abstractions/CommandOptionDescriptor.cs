namespace Terminal.Gui.Cli;

public sealed record CommandOptionDescriptor (
    string Name,
    string? ShortName,
    Type ValueType,
    string Description,
    bool Required,
    string? DefaultValue);
