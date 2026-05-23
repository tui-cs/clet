namespace Terminal.Gui.Cli;

/// <summary>Configuration options for the host.</summary>
public sealed class CliHostOptions
{
    /// <summary>Application name shown in --help and --version.</summary>
    public string ApplicationName { get; set; } = "app";

    /// <summary>Version string (shown in --version output).</summary>
    public string? Version { get; set; }

    /// <summary>Custom help provider. Null = auto-generated from metadata.</summary>
    public IHelpProvider? HelpProvider { get; set; }

    /// <summary>Maximum characters allowed for --initial. Default: 64K.</summary>
    public int MaxInitialChars { get; set; } = 64 * 1024;

    /// <summary>
    /// Embedded resource name (or literal content) for the agent-guide verb.
    /// When set, the agent-guide command is registered. When null, it is not.
    /// </summary>
    public string? AgentGuide { get; set; }

    /// <summary>
    /// If true, AgentGuide is treated as an embedded resource name to load
    /// from the consumer's assembly. If false, it's literal content.
    /// </summary>
    public bool AgentGuideIsResource { get; set; } = true;

    /// <summary>
    /// Consumer-defined global options. These are parsed by the framework and
    /// placed into CommandRunOptions.Extensions.
    /// </summary>
    public List<GlobalOptionDescriptor> GlobalOptions { get; } = [];
}
