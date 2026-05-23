namespace Terminal.Gui.Cli;

/// <summary>Pluggable help rendering.</summary>
public interface IHelpProvider
{
    /// <summary>Render root-level --help. Return null to use auto-generated.</summary>
    string? GetRootHelp (ICommandRegistry registry);

    /// <summary>Render per-command help. Return null to use auto-generated.</summary>
    string? GetCommandHelp (ICliCommand command);
}
