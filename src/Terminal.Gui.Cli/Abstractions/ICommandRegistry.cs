namespace Terminal.Gui.Cli;

/// <summary>Manages alias → command lookup.</summary>
public interface ICommandRegistry
{
    void Register (ICliCommand command);
    bool TryResolve (string alias, out ICliCommand? command);
    IReadOnlyCollection<ICliCommand> All { get; }
}
