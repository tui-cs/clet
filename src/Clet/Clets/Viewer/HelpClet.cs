using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Command = Terminal.Gui.Input.Command;

namespace Clet;

internal sealed class HelpClet (ICommandRegistry registry) : IViewerCommand
{
    public string PrimaryAlias => "help";
    public IReadOnlyList<string> Aliases => ["help"];
    public string Description => "Shows help for clet commands.";
    public CommandKind Kind => CommandKind.Viewer;
    public Type ResultType => typeof (void);
    public bool AcceptsPositionalArgs => true;

    public IReadOnlyList<CommandOptionDescriptor> Options => [];

    /// <summary>Handles --cat mode without TUI init. Called by the dispatcher.</summary>
    public Task<CommandResult?> RenderCatAsync (CommandRunOptions options, TextWriter stdout, CancellationToken cancellationToken)
    {
        string? alias = options.Arguments.Count > 0 ? options.Arguments[0] : null;

        if (alias is not null and not "help" && !registry.TryResolve (alias, out _))
        {
            return Task.FromResult<CommandResult?> (new CommandResult (CommandStatus.Error, null, "usage", $"Unknown alias '{alias}'. Try 'clet list' to see available clets."));
        }

        (string markdown, _) = BuildHelpContent (alias);
        MarkdownHelpRenderer.RenderToAnsi (markdown, stdout);

        return Task.FromResult<CommandResult?> (new CommandResult (CommandStatus.Ok, null, null, null));
    }

    public async Task<CommandResult> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new (CommandStatus.Cancelled, null, null, null);
        }

        string? alias = options.Arguments.Count > 0 ? options.Arguments[0] : null;

        // Validate alias early — unknown aliases should error, not render
        if (alias is not null and not "help" && !registry.TryResolve (alias, out _))
        {
            return new (CommandStatus.Error, null, "usage", $"Unknown alias '{alias}'. Try 'clet list' to see available clets.");
        }

        (string markdown, string title) = BuildHelpContent (alias);

        // --cat mode: render to stdout and exit
        if (options.Cat)
        {
            MarkdownHelpRenderer.RenderToAnsi (markdown, Console.Out);

            return new (CommandStatus.Ok, null, null, null);
        }

        // --- Build TUI ---

        bool browseMode = !options.HasExtension ("no-browse");
        BrowseBar? browseBar = null;

        Runnable window = new ()
        {
            Title = title,
            Width = Dim.Fill (),
            Height = Dim.Fill (),
        };

        Markdown markdownView = new ()
        {
            Width = Dim.Fill (),
            Height = Dim.Fill (1),
            SyntaxHighlighter = new TextMateSyntaxHighlighter (),
        };

        markdownView.ViewportSettings |= ViewportSettingsFlags.HasHorizontalScrollBar;

        Shortcut statusShortcut = new (Key.Empty, title, action: null) { MouseHighlightStates = MouseState.None };

        if (browseMode)
        {
            string key = alias ?? "(overview)";
            browseBar = new BrowseBar (key)
            {
                OnNavigate = NavigateTo,
            };
        }

        markdownView.LinkClicked += (_, e) =>
        {
            LinkNavigationHelper.HandleLinkClicked (
                e,
                customSchemeHandler: url =>
                {
                    if (!url.StartsWith ("clet:help", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    string? linkAlias = url.Length > "clet:help:".Length
                        ? url["clet:help:".Length..]
                        : null;

                    string key = linkAlias ?? "(overview)";
                    browseBar?.Push (key);
                    NavigateTo (key);

                    return true;
                },
                openHttpLinks: true,
                statusUpdater: url => statusShortcut.Title = url);
        };

        List<Shortcut> statusItems =
        [
            new (Application.GetDefaultKey (Command.Quit), "Quit", window.RequestStop),
        ];

        if (browseBar is not null)
        {
            statusItems.Insert (0, browseBar.Forward);
            statusItems.Insert (0, browseBar.Back);
        }

        statusItems.Add (statusShortcut);

        StatusBar statusBar = new (statusItems)
        {
            AlignmentModes = AlignmentModes.IgnoreFirstOrLast,
        };
        browseBar?.ApplyStyle ();

        window.Add (markdownView, statusBar);

        window.Initialized += (_, _) =>
        {
            markdownView.Text = markdown;

            // Adding links to table cells causes the Markdown view to auto-scroll
            // to a focused link after layout. Use a counter to reset viewport on
            // the second draw (after the focus-scroll has run).
            int drawCount = 0;

            markdownView.DrawComplete += ResetViewport;

            void ResetViewport (object? sender, DrawEventArgs e)
            {
                drawCount++;

                if (drawCount >= 2)
                {
                    markdownView.DrawComplete -= ResetViewport;
                    markdownView.Viewport = markdownView.Viewport with { Y = 0 };
                }
            }
        };

        void NavigateTo (string key)
        {
            string? targetAlias = key == "(overview)" ? null : key;
            (string md, string t) = BuildHelpContent (targetAlias);
            markdownView.Text = md;
            window.Title = t;
            statusShortcut.Title = t;
        }

        try
        {
            await app.RunAsync (window, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new (CommandStatus.Cancelled, null, null, null);
        }

        return new (CommandStatus.Ok, null, null, null);
    }

    private (string Markdown, string Title) BuildHelpContent (string? alias)
    {
        if (alias is null)
        {
            return BuildOverview ();
        }

        if (alias is "help")
        {
            string helpMd = "# clet help\n\nNavigating clet's built-in help system.\n\n";
            string? helpExtra = MarkdownHelpRenderer.ReadEmbeddedHelp ("help.md");

            if (helpExtra is not null)
            {
                helpMd += helpExtra;
            }

            helpMd += "\n\n---\n\n[Back to overview](clet:help)\n";

            return (helpMd, "clet help");
        }

        if (!registry.TryResolve (alias, out ICliCommand? clet) || clet is null)
        {
            return ($"# Unknown clet: {alias}\n\nTry `clet list` to see available clets.", "clet help");
        }

        string md = MarkdownHelpRenderer.BuildAliasHelpMarkdown (clet);
        md += "\n\n---\n\n[Back to overview](clet:help)\n";

        return (md, $"clet help {clet.PrimaryAlias}");
    }

    private (string Markdown, string Title) BuildOverview ()
    {
        string? rawMarkdown = MarkdownHelpRenderer.ReadEmbeddedHelp ("overview.md");

        if (rawMarkdown is null)
        {
            return ("# clet\n\nNo overview available.", "clet");
        }

        string cletTable = MarkdownHelpRenderer.BuildCletTableMarkdown (registry).TrimEnd ();
        string markdown = rawMarkdown.Replace ("{{CLET_TABLE}}", cletTable);
        markdown = markdown.Replace ("{{VERSION}}", $"v{GetVersion ()} (Terminal.Gui {GetTerminalGuiVersion ()})");

        return (markdown, "clet");
    }

    private static string GetVersion () => VersionInfo.GetCletVersion ();

    private static string GetTerminalGuiVersion () => VersionInfo.GetTerminalGuiVersion ();
}
