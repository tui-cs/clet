namespace Terminal.Gui.Cli;

/// <summary>
/// Parsed options bag passed to commands. The library populates the framework
/// properties it owns; consumer-defined options flow through the Extensions
/// dictionary. Commands access both via this single type.
/// </summary>
public sealed class CommandRunOptions
{
    // --- Framework-owned (the library parses and acts on these) ---

    /// <summary>Pre-fill value for the View.</summary>
    public string? Initial { get; init; }

    /// <summary>Title override for TUI chrome.</summary>
    public string? Title { get; init; }

    /// <summary>Whether to emit JSON envelope instead of plain text.</summary>
    public bool JsonOutput { get; init; }

    /// <summary>Cancel after this duration (parsed from --timeout).</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Force fullscreen (input commands default to inline).</summary>
    public bool Fullscreen { get; init; }

    /// <summary>Render markdown as ANSI to stdout instead of launching the interactive viewer.</summary>
    public bool Cat { get; init; }

    /// <summary>Write result to file instead of stdout.</summary>
    public string? OutputPath { get; init; }

    /// <summary>Constrain inline height.</summary>
    public int? Rows { get; init; }

    /// <summary>Positional arguments (after alias, before options).</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    // --- Per-command options (declared via CommandOptionDescriptor) ---

    /// <summary>Per-command option values keyed by option name.</summary>
    public IReadOnlyDictionary<string, string> CommandOptions { get; init; }
        = new Dictionary<string, string> ();

    // --- Consumer-defined global options (registered via CliHostOptions) ---

    /// <summary>
    /// Extensible bag for consumer-registered global options. Keyed by the
    /// option name (without leading dashes). Each key maps to a list of values
    /// to support repeatable options (e.g. --allow-file path1 --allow-file path2).
    /// For boolean flags, the list contains a single empty string (presence = true).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Extensions { get; init; }
        = new Dictionary<string, IReadOnlyList<string>> ();

    /// <summary>Typed accessor for single-value extension options.</summary>
    public T? GetExtension<T> (string key, Func<string, T> parser, T? defaultValue = default)
    {
        return Extensions.TryGetValue (key, out IReadOnlyList<string>? values) && values.Count > 0
            ? parser (values[^1])
            : defaultValue;
    }

    /// <summary>Accessor for repeatable extension options (returns all values).</summary>
    public IReadOnlyList<string> GetExtensionList (string key)
    {
        return Extensions.TryGetValue (key, out IReadOnlyList<string>? values)
            ? values
            : [];
    }

    /// <summary>Boolean flag accessor (present = true).</summary>
    public bool HasExtension (string key) => Extensions.ContainsKey (key);
}
