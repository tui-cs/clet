using Terminal.Gui.App;

namespace Terminal.Gui.Cli.Tests;

/// <summary>Minimal ICliCommand implementation for testing.</summary>
internal sealed class StubCommand (string primaryAlias, IReadOnlyList<string> aliases) : ICliCommand
{
    public string PrimaryAlias => primaryAlias;
    public IReadOnlyList<string> Aliases => aliases;
    public string Description => $"Stub command: {primaryAlias}";
    public CommandKind Kind => CommandKind.Input;
    public Type ResultType => typeof (string);
    public IReadOnlyList<CommandOptionDescriptor> Options => [];

    public Task<CommandResult> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken)
    {
        return Task.FromResult (new CommandResult (CommandStatus.Ok, initial ?? "default", null, null));
    }
}
