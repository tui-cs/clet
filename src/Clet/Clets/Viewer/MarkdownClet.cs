using System.Collections.ObjectModel;
using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TextMateSharp.Grammars;
using Command = Terminal.Gui.Input.Command;

namespace Clet;

internal sealed class MarkdownClet : IViewerCommand
{
    public string PrimaryAlias => "md";
    public IReadOnlyList<string> Aliases => ["md", "markdown"];
    public string Description => "Browse and render Markdown files with link navigation and syntax highlighting.";
    public CommandKind Kind => CommandKind.Viewer;
    public Type ResultType => typeof (void);

    public IReadOnlyList<CommandOptionDescriptor> Options =>
    [
        new ("theme", "t", typeof (string),
            $"Syntax-highlighting theme. Available: {string.Join (", ", Enum.GetNames<ThemeName> ())}",
            false, nameof (ThemeName.DarkPlus)),
    ];

    public bool AcceptsPositionalArgs => true;

    public Task<CommandResult?> RenderCatAsync (CommandRunOptions options, TextWriter stdout, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult<CommandResult?> (new CommandResult (CommandStatus.Cancelled, null, null, null));
        }

        // Resolve content
        TextReader? stdinReader = Console.IsInputRedirected ? Console.In : null;
        MarkdownContentResolver.ResolveResult resolved = MarkdownContentResolver.Resolve (null, options, stdinReader);

        if (!resolved.IsSuccess)
        {
            return Task.FromResult<CommandResult?> (new CommandResult (CommandStatus.Error, null, resolved.ErrorCode, resolved.ErrorMessage));
        }

        string? content = resolved.Content;

        if (string.IsNullOrEmpty (content))
        {
            return Task.FromResult<CommandResult?> (new CommandResult (CommandStatus.Error, null, "io", "No content to render."));
        }

        MarkdownHelpRenderer.RenderToAnsi (content, stdout);

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

        // Resolve content: file args → inline content → stdin → error
        TextReader? stdinReader = Console.IsInputRedirected ? Console.In : null;
        MarkdownContentResolver.ResolveResult resolved = MarkdownContentResolver.Resolve (initial, options, stdinReader);

        if (!resolved.IsSuccess)
        {
            return new (CommandStatus.Error, null, resolved.ErrorCode, resolved.ErrorMessage);
        }

        List<string> files = resolved.Files;
        string? content = initial;

        if (resolved.Content is not null)
        {
            content = resolved.Content;
        }

        // Track current file directory for resolving relative links
        string? currentFileDir = files.Count > 0 ? Path.GetDirectoryName (Path.GetFullPath (files[0])) : null;

        // File access policy for link navigation (reuse the same confinement as file loading)
        IReadOnlyList<string> allowedFiles = options.GetExtensionList ("allow-file");
        FileAccessPolicy linkPolicy = new (
            Directory.GetCurrentDirectory (),
            allowedFiles.Count > 0 ? allowedFiles : null,
            options.HasExtension ("allow-binary"));

        // Browser mode
        bool browseMode = !options.HasExtension ("no-browse");
        string? currentFile = files.Count > 0 ? Path.GetFullPath (files[0]) : null;
        BrowseBar? browseBar = null;

        // Parse --theme option
        ThemeName syntaxTheme = ThemeName.DarkPlus;

        if (options.CommandOptions.TryGetValue ("theme", out string? themeStr)
            && Enum.TryParse (themeStr, ignoreCase: true, out ThemeName parsed))
        {
            syntaxTheme = parsed;
        }

        Runnable window = new ()
        {
            Title = options.Title ?? "Markdown Browser",
            Width = Dim.Fill (),
            Height = Dim.Fill (),
        };

        Markdown markdownView = new ()
        {
            Width = Dim.Fill (),
            Height = Dim.Fill (1), // leave room for StatusBar
            SyntaxHighlighter = new TextMateSyntaxHighlighter (syntaxTheme),
        };

        markdownView.ViewportSettings |= ViewportSettingsFlags.HasHorizontalScrollBar;

        // --- StatusBar items (declared early so local functions can capture them) ---

        Shortcut lineCountShortcut = new () { Title = "0 lines", MouseHighlightStates = MouseState.None, Enabled = false };
        Shortcut fileSizeShortcut = new () { Title = "0 B", MouseHighlightStates = MouseState.None, Enabled = false };

        // Status link — shows the current filename or a clickable URL when the user
        // hovers/clicks a hyperlink in the markdown. Clicking the link in the status
        // bar opens it in the default browser.
        Link statusLink = new () { Text = "Ready", CanFocus = false };
        Shortcut statusShortcut = new () { CommandView = statusLink, MouseHighlightStates = MouseState.None };

        // Browser mode: back/forward shortcuts for bottom StatusBar
        if (browseMode)
        {
            browseBar = new BrowseBar (currentFile)
            {
                OnNavigate = path => LoadFile (path),
            };
        }

        // --- MarkdownView event wiring ---

        markdownView.LinkClicked += (_, e) =>
        {
            LinkNavigationHelper.HandleLinkClicked (
                e,
                customSchemeHandler: url =>
                {
                    if (!browseMode)
                    {
                        return false;
                    }

                    // Navigate local .md files within the sandbox
                    if (currentFileDir is not null
                        && TryResolveLocalMarkdownLink (url, currentFileDir, linkPolicy, out string? resolvedPath, out string? fragment)
                        && resolvedPath is not null)
                    {
                        browseBar!.Push (resolvedPath);
                        LoadFile (resolvedPath, fragment);

                        return true;
                    }

                    return false;
                },
                openHttpLinks: true,
                statusUpdater: url =>
                {
                    statusLink.Text = url;
                    statusLink.Url = url;
                    statusShortcut.MouseHighlightStates = MouseState.In;
                });
        };

        markdownView.SubViewsLaidOut += (_, _) =>
        {
            lineCountShortcut.Title = $"{markdownView.LineCount} lines";
        };

        // --- Build StatusBar ---

        List<Shortcut> statusItems =
        [
            new (Application.GetDefaultKey (Command.Quit), "Quit", window.RequestStop),
        ];

        if (browseBar is not null)
        {
            statusItems.Insert (0, browseBar.Forward);
            statusItems.Insert (0, browseBar.Back);
        }

        // Theme selector
        DropDownList<ThemeName> themeDropDown = new () { Value = syntaxTheme, CanFocus = false };

        themeDropDown.ValueChanged += (_, e) =>
        {
            if (e.Value is not { } themeName)
            {
                return;
            }

            markdownView.SyntaxHighlighter = new TextMateSyntaxHighlighter (themeName);
        };

        statusItems.Add (new Shortcut { Title = "Theme", CommandView = themeDropDown });

        // Theme background toggle
        CheckBox themeBgCheckBox = new ()
        {
            Text = "Theme _BG",
            Value = markdownView.UseThemeBackground ? CheckState.Checked : CheckState.UnChecked,
        };

        themeBgCheckBox.ValueChanged += (_, e) =>
        {
            markdownView.UseThemeBackground = e.NewValue == CheckState.Checked;
        };

        statusItems.Add (new Shortcut { CommandView = themeBgCheckBox });
        statusItems.AddRange ([lineCountShortcut, fileSizeShortcut, statusShortcut]);

        // File selector when multiple files are provided
        if (files.Count > 1)
        {
            // Use basenames when they are all distinct; fall back to relative paths
            // so that files like a/readme.md and b/readme.md get unique labels.
            List<string> basenames = [.. files.Select (static f => Path.GetFileName (f.AsSpan ()).ToString ())];
            bool hasCollisions = basenames.Count != basenames.Distinct (StringComparer.OrdinalIgnoreCase).Count ();
            string cwd = Directory.GetCurrentDirectory ();
            List<string> displayNames = hasCollisions
                ? [.. files.Select (f => Path.GetRelativePath (cwd, f))]
                : basenames;

            ObservableCollection<string> displayNamesOc = new (displayNames);

            DropDownList fileSelector = new ()
            {
                Source = new ListWrapper<string> (displayNamesOc),
                ReadOnly = true,
                Text = displayNames[0],
                Width = 30,
            };

            fileSelector.ValueChanged += (_, _) =>
            {
                // Use the unique display name list — since labels are guaranteed distinct,
                // IndexOf is unambiguous even when files share the same basename.
                int index = displayNames.IndexOf (fileSelector.Text);

                if (index < 0 || index >= files.Count)
                {
                    return;
                }

                browseBar?.Push (files[index]);
                LoadFile (files[index]);
            };

            Shortcut fileSelectorShortcut = new () { CommandView = fileSelector, HelpText = "File" };
            statusItems.Insert (1, fileSelectorShortcut);
        }

        StatusBar statusBar = new (statusItems) { AlignmentModes = AlignmentModes.IgnoreFirstOrLast };
        browseBar?.ApplyStyle ();

        window.Add (markdownView, statusBar);

        // Load content after initial layout
        window.Initialized += (_, _) =>
        {
            if (files.Count > 0)
            {
                LoadFile (files[0]);
            }
            else if (!string.IsNullOrEmpty (content))
            {
                string sanitized = SanitizeContent (content);
                markdownView.Text = sanitized;
                fileSizeShortcut.Title = FormatFileSize (System.Text.Encoding.UTF8.GetByteCount (sanitized));
                statusLink.Text = options.Title ?? "(inline)";
                statusLink.Url = string.Empty;
                statusShortcut.MouseHighlightStates = MouseState.None;
            }
        };

        try
        {
            await app.RunAsync (window, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new (CommandStatus.Cancelled, null, null, null);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new (CommandStatus.Cancelled, null, null, null);
        }

        return new (CommandStatus.Ok, null, null, null);

        void LoadFile (string filePath, string? fragment = null)
        {
            string fullPath = Path.GetFullPath (filePath);

            string fileContent = SanitizeContent (File.ReadAllText (fullPath));
            markdownView.Text = fileContent;

            currentFile = fullPath;
            currentFileDir = Path.GetDirectoryName (fullPath);

            FileInfo fileInfo = new (fullPath);
            fileSizeShortcut.Title = FormatFileSize (fileInfo.Length);
            statusLink.Text = Path.GetFileName (fullPath);
            statusLink.Url = string.Empty;

            if (!string.IsNullOrEmpty (fragment))
            {
                markdownView.ScrollToAnchor (fragment);
            }
        }

    }

    /// <summary>
    /// Resolves a link URL to a local markdown file path if it passes the file access policy.
    /// </summary>
    internal static bool TryResolveLocalMarkdownLink (
        string url,
        string currentDir,
        FileAccessPolicy policy,
        out string? resolvedPath,
        out string? fragment)
    {
        resolvedPath = null;
        fragment = null;

        // Extract fragment (e.g. #section) before resolving the path
        int fragmentIndex = url.IndexOf ('#');
        string pathPart = fragmentIndex >= 0 ? url[..fragmentIndex] : url;

        if (fragmentIndex >= 0)
        {
            fragment = url[(fragmentIndex + 1)..];
        }

        if (string.IsNullOrWhiteSpace (pathPart))
        {
            return false;
        }

        // Handle file:// URIs
        if (pathPart.StartsWith ("file://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate (pathPart, UriKind.Absolute, out Uri? fileUri) || !fileUri.IsFile)
            {
                return false;
            }

            pathPart = fileUri.LocalPath;
        }
        // Reject non-local schemes (http://, https://, mailto:, etc.)
        else if (pathPart.Contains ("://", StringComparison.Ordinal))
        {
            return false;
        }

        string fullPath;

        try
        {
            fullPath = Path.IsPathRooted (pathPart)
                ? Path.GetFullPath (pathPart)
                : Path.GetFullPath (Path.Combine (currentDir, pathPart));
        }
        catch
        {
            return false;
        }

        if (!File.Exists (fullPath))
        {
            return false;
        }

        // Delegate all security checks (extension, cwd confinement, binary, size) to the policy
        if (policy.CheckFile (fullPath) is not null)
        {
            return false;
        }

        resolvedPath = fullPath;

        return true;
    }

    private static string FormatFileSize (long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    private static string SanitizeContent (string content)
    {
        return TerminalEscapeSanitizer.Sanitize (content) ?? string.Empty;
    }
}
