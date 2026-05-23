using System.Reflection;
using System.Text;
using Terminal.Gui.Cli;
using Terminal.Gui.Drawing;
using Terminal.Gui.Views;

namespace Clet;

/// <summary>
/// Renders Markdown content as ANSI to a <see cref="TextWriter"/> (print mode).
/// Uses <see cref="Markdown.RenderToAnsi"/> for headless rendering.
/// </summary>
internal static class MarkdownHelpRenderer
{
    /// <summary>
    /// Renders the given markdown string to ANSI escape sequences and writes them to <paramref name="output"/>.
    /// </summary>
    public static void RenderToAnsi (string markdown, TextWriter output)
    {
        // Sanitize input markdown to remove terminal escape sequences from untrusted content
        markdown = TerminalEscapeSanitizer.Sanitize (markdown)!;

        // Force UTF-8 on Windows where Console.OutputEncoding defaults to OEM code page.
        // Only mutate when output is Console.Out and stdout isn't redirected.
        Encoding? previousEncoding = null;
        TextWriter target = output;

        if (ReferenceEquals (output, Console.Out) && !Console.IsOutputRedirected)
        {
            previousEncoding = Console.OutputEncoding;
            Console.OutputEncoding = Encoding.UTF8;
            target = Console.Out;
        }

        int width;

        try
        {
            width = Console.WindowWidth;
        }
        catch
        {
            width = 0;
        }

        if (width <= 0)
        {
            width = 80;
        }

        try
        {
            Markdown markdownView = new ()
            {
                SyntaxHighlighter = new TextMateSyntaxHighlighter (),
                UseThemeBackground = false,
            };

            string rendered = markdownView.RenderToAnsi (markdown, width);

            // Final pass: strip any user-payload escape sequences that survived through TG rendering
            // while preserving the renderer's own SGR sequences.
            rendered = TerminalEscapeSanitizer.SanitizeRenderedOutput (rendered);
            target.WriteLine (rendered);
        }
        finally
        {
            if (previousEncoding is not null)
            {
                Console.OutputEncoding = previousEncoding;
            }
        }
    }

    /// <summary>
    /// Reads an embedded markdown resource from the <c>Help/</c> directory.
    /// Returns <c>null</c> if the resource is not found.
    /// </summary>
    public static string? ReadEmbeddedHelp (string resourceSuffix)
    {
        Assembly assembly = typeof (MarkdownHelpRenderer).Assembly;
        string? resourceName = assembly.GetManifestResourceNames ()
            .FirstOrDefault (n => n.EndsWith (resourceSuffix, StringComparison.Ordinal));

        if (resourceName is null)
        {
            return null;
        }

        using Stream stream = assembly.GetManifestResourceStream (resourceName)!;
        using StreamReader reader = new (stream);

        return reader.ReadToEnd ();
    }

    /// <summary>
    /// Generates a Markdown string for a clet's help page from its <see cref="ICliCommand"/> metadata.
    /// </summary>
    public static string BuildAliasHelpMarkdown (ICliCommand clet)
    {
        StringBuilder sb = new ();
        sb.AppendLine ($"# clet {clet.PrimaryAlias}");
        sb.AppendLine ();
        sb.AppendLine (clet.Description);
        sb.AppendLine ();
        sb.AppendLine ($"**Kind:** {(clet.Kind == CommandKind.Input ? "input" : "viewer")}");
        sb.AppendLine ();
        sb.AppendLine ($"**Result type:** {ResultTypeName (clet.ResultType)}");

        if (clet.Aliases.Count > 1)
        {
            sb.AppendLine ();
            sb.AppendLine ($"**Aliases:** {string.Join (", ", clet.Aliases.Select (a => $"`{a}`"))}");
        }

        if (clet.Options.Count > 0)
        {
            sb.AppendLine ();
            sb.AppendLine ("## Options");
            sb.AppendLine ();
            sb.AppendLine ("| Option | Type | Description | Required | Default |");
            sb.AppendLine ("|--------|------|-------------|----------|---------|");

            foreach (CommandOptionDescriptor opt in clet.Options)
            {
                string name = opt.ShortName is null
                    ? $"`--{opt.Name}`"
                    : $"`--{opt.Name}`, `-{opt.ShortName}`";
                string required = opt.Required ? "yes" : "no";
                string defaultVal = opt.DefaultValue ?? "-";
                sb.AppendLine ($"| {name} | {ResultTypeName (opt.ValueType)} | {opt.Description} | {required} | {defaultVal} |");
            }
        }

        // Append embedded help content (examples, notes) if available
        string? extra = ReadEmbeddedHelp ($"{clet.PrimaryAlias}.md");

        if (extra is not null)
        {
            sb.AppendLine ();
            sb.Append (extra);
        }

        return sb.ToString ();
    }

    /// <summary>
    /// Generates a Markdown table of all registered clets with aliases shown inline.
    /// </summary>
    public static string BuildCletTableMarkdown (ICommandRegistry registry)
    {
        StringBuilder sb = new ();
        sb.AppendLine ("## Available Clets");
        sb.AppendLine ();
        sb.AppendLine ("| Alias | Description | Options |");
        sb.AppendLine ("|-------|-------------|---------|");

        foreach (ICliCommand clet in registry.All)
        {
            string aliases = clet.Aliases.Count <= 1
                ? $"[{clet.PrimaryAlias}](clet:help:{clet.PrimaryAlias})"
                : string.Join (", ", clet.Aliases.Select (a => $"[{a}](clet:help:{a})"));

            string options = BuildOptionsColumn (clet);

            sb.AppendLine ($"| {aliases} | {clet.Description} | {options} |");
        }

        return sb.ToString ();
    }

    private static string BuildOptionsColumn (ICliCommand clet)
    {
        List<string> parts = new ();

        foreach (CommandOptionDescriptor opt in clet.Options)
        {
            parts.Add ($"`--{opt.Name}`");
        }

        if (clet.AcceptsPositionalArgs)
        {
            parts.Add ("`args...`");
        }

        return parts.Count == 0 ? "" : string.Join (", ", parts);
    }

    private static string ResultTypeName (Type type) => TypeNames.WireName (type);
}
