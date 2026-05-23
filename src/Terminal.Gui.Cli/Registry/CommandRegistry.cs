namespace Terminal.Gui.Cli;

/// <summary>Default implementation. Case-insensitive, duplicate-rejecting.</summary>
public sealed class CommandRegistry : ICommandRegistry
{
    private readonly Dictionary<string, ICliCommand> _byAlias = new (StringComparer.OrdinalIgnoreCase);
    private readonly List<ICliCommand> _all = [];

    public void Register (ICliCommand command)
    {
        foreach (string alias in command.Aliases)
        {
            if (!_byAlias.TryAdd (alias, command))
            {
                throw new InvalidOperationException ($"Duplicate alias '{alias}' is already registered.");
            }
        }

        _all.Add (command);
    }

    public bool TryResolve (string alias, out ICliCommand? command)
    {
        return _byAlias.TryGetValue (alias, out command);
    }

    public IReadOnlyCollection<ICliCommand> All => _all.AsReadOnly ();
}
