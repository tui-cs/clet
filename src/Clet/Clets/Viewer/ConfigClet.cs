using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualBasic;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Editor;
using Terminal.Gui.Editor.Document;
using Terminal.Gui.Editor.Highlighting;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Command = Terminal.Gui.Input.Command;
using Terminal.Gui.Cli;

namespace Clet;

internal sealed class ConfigClet : IViewerCommand
{
    /// <summary>The config file name inside ~/.tui/.</summary>
    internal const string ConfigFileName = "clet.config.json";

    public string PrimaryAlias => "config";
    public IReadOnlyList<string> Aliases => ["config"];
    public string Description => "Edit the clet configuration file (~/.tui/clet.config.json).";
    public CommandKind Kind => CommandKind.Viewer;
    public Type ResultType => typeof (void);

    public IReadOnlyList<CommandOptionDescriptor> Options => [];

    public async Task<CommandResult> RunAsync (
        IApplication app,
        string? content,
        CommandRunOptions options,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new (CommandStatus.Cancelled, default, null, null);
        }

        string configPath = GetConfigPath ();
        EnsureConfigFile (configPath);

        // ConfigureAwait (false): resuming on an ambient SynchronizationContext and
        // then calling IApplication.RunAsync deadlocks in Terminal.Gui 2.5.0-preview
        // (the run loop never completes; observed under the xunit sync context).
        // Upstream issue tracked with tui-cs/Terminal.Gui#5416 validation.
        string configText = await File.ReadAllTextAsync (configPath, cancellationToken).ConfigureAwait (false);

        // Check for pre-existing config errors to show on launch
        string? launchError = ValidateConfig (configPath);

        bool isDirty = false;
        Shortcut statusMessage = new () { Title = "Ready", MouseHighlightStates = MouseState.None, Enabled = false };
        Shortcut cursorPosition = new () { Title = "Ln 1, Col 1", MouseHighlightStates = MouseState.None, Enabled = false };

        Runnable window = new ()
        {
            Title = options.Title ?? $"clet config — {configPath}",
            Width = Dim.Fill (),
            Height = Dim.Fill (),
            BorderStyle = LineStyle.None,
        };

        Editor editor = new ()
        {
            Width = Dim.Fill (),
            Height = Dim.Fill (1), // leave room for StatusBar
            ConvertTabsToSpaces = true,
            IndentationSize = 2,
            GutterOptions = GutterOptions.LineNumbers,
            ViewportSettings = ViewportSettingsFlags.HasScrollBars,
        };

        editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinitionByExtension (".json");

        editor.Document = new TextDocument (configText);

        editor.CaretChanged += (_, _) =>
        {
            TextDocument? document = editor.Document;

            if (document is not null)
            {
                DocumentLine line = document.GetLineByOffset (editor.CaretOffset);
                cursorPosition.Title = $"Ln {line.LineNumber}, Col {editor.CaretOffset - line.Offset + 1}";
            }
        };

        editor.Document.TextChanged += (_, _) =>
        {
            isDirty = true;
            UpdateTitle ();
        };

        // --- Theme selector ---

        ImmutableList<string> themeNames = ThemeManager.GetThemeNames ();
        ObservableCollection<string> themeCollection = new (themeNames);

        DropDownList themeDropDown = new ()
        {
            Source = new ListWrapper<string> (themeCollection),
            ReadOnly = true,
            Text = ThemeManager.Theme,
            Width = Dim.Auto (DimAutoStyle.Text, minimumContentDim: 10),
        };

        themeDropDown.ValueChanged += (_, _) =>
        {
            string selected = themeDropDown.Text;

            if (!string.IsNullOrEmpty (selected) && selected != ThemeManager.Theme)
            {
                ThemeManager.Theme = selected;
            }
        };

        // --- StatusBar ---

        Shortcut saveShortcut = new (Key.S.WithCtrl, Terminal.Gui.Resources.Strings.cmdSave, Save);
        Shortcut quitShortcut = new (Application.GetDefaultKey (Command.Quit), Terminal.Gui.Resources.Strings.cmdQuit, TryQuit);

        StatusBar statusBar = new ([quitShortcut, saveShortcut, statusMessage, cursorPosition, new Shortcut { Title = "Theme", CommandView = themeDropDown }])
        {
            AlignmentModes = AlignmentModes.IgnoreFirstOrLast,
        };

        window.Add (editor, statusBar);

        window.Initialized += (_, _) =>
        {
            editor.SetFocus ();

            if (launchError is not null)
            {
                MessageBox.ErrorQuery (
                    app,
                    "Configuration Error",
                    launchError,
                    Terminal.Gui.Resources.Strings.btnOk);

                statusMessage.Title = "Config has errors";
            }
        };

        // Override Quit to prompt on unsaved changes
        window.KeyDown += (_, e) =>
        {
            if (e.KeyCode == (Application.GetDefaultKey (Command.Quit).KeyCode))
            {
                e.Handled = true;
                TryQuit ();
            }
        };

        try
        {
            await app.RunAsync (window, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new (CommandStatus.Cancelled, default, null, null);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new (CommandStatus.Cancelled, default, null, null);
        }

        return new (CommandStatus.Ok, null, null, null);

        void UpdateTitle ()
        {
            string marker = isDirty ? " •" : "";
            window.Title = $"clet config — {configPath}{marker}";
        }

        void Save ()
        {
            Logging.Information ("ConfigClet: Save triggered");

            try
            {
                File.WriteAllText (configPath, editor.Document?.Text ?? string.Empty);
                isDirty = false;
                UpdateTitle ();
                Logging.Information ("ConfigClet: file written successfully");
            }
            catch (Exception ex)
            {
                Logging.Error ($"ConfigClet: file write failed: {ex.Message}");
                statusMessage.Title = $"Save failed: {ex.Message}";

                return;
            }

            // Check JSON syntax first so errors carry line/column info, then
            // reload the MEC-based configuration. The builder never throws on
            // bad sources — per-source errors are collected in TuiJsonErrors
            // and the library falls back to defaults on its own.
            try
            {
                ParseJsonc (editor.Document?.Text ?? string.Empty);
            }
            catch (JsonException jsonEx)
            {
                Logging.Error ($"ConfigClet: config has a JSON syntax error: {jsonEx.Message}");
                ShowJsonErrorDialog (jsonEx);
                statusMessage.Title = "Saved with errors";

                return;
            }

            CletConfiguration.Reload ();
            string? applyError = ConsumeConfigErrors ()
                                 ?? CheckUnknownTheme (editor.Document?.Text ?? string.Empty);

            if (applyError is not null)
            {
                Logging.Error ($"ConfigClet: config reload reported: {applyError}");
                ShowConfigErrorDialog (applyError);
                statusMessage.Title = "Saved with errors";

                return;
            }

            Logging.Information ("ConfigClet: config reloaded and applied successfully");
            statusMessage.Title = "Saved ✓";
        }

        void ShowJsonErrorDialog (JsonException ex)
        {
            // Extract line number (0-based in JsonException)
            int errorRow = ex.LineNumber.HasValue ? (int)ex.LineNumber.Value : 0;
            int errorCol = ex.BytePositionInLine.HasValue ? (int)ex.BytePositionInLine.Value : 0;

            string details = ex.Message;

            // Strip the redundant " Path: ... | LineNumber: ... | ..." suffix that JsonException adds
            int pathIdx = details.IndexOf (" Path:", StringComparison.Ordinal);

            if (pathIdx > 0)
            {
                details = details[..pathIdx];
            }

            string message = $"{details}\n\nLine {errorRow + 1}, Column {errorCol + 1}";

            MessageBox.ErrorQuery (
                app,
                "Configuration Error",
                message,
                "Go to Error");

            // Navigate the cursor to the error location
            TextDocument? document = editor.Document;

            if (document is not null && errorRow < document.LineCount)
            {
                DocumentLine line = document.GetLineByNumber (errorRow + 1); // 1-based
                int offset = line.Offset + Math.Min (errorCol, line.Length);
                editor.CaretOffset = offset;
            }

            editor.SetFocus ();
        }

        void ShowConfigErrorDialog (string message)
        {
            MessageBox.ErrorQuery (
                app,
                "Configuration Error",
                message,
                Terminal.Gui.Resources.Strings.btnOk);

            editor.SetFocus ();
        }

        void TryQuit ()
        {
            if (isDirty)
            {
                int? result = MessageBox.Query (
                    app,
                    "Unsaved Changes",
                    "You have unsaved changes. Save before quitting?",
                    Terminal.Gui.Resources.Strings.btnCancel,
                    Terminal.Gui.Resources.Strings.btnNo,
                    "_Save & Quit");

                switch (result)
                {
                    case 0:
                        break;

                    case 1:
                        window.RequestStop ();

                        break;
                    case 2:
                        Save ();
                        window.RequestStop ();

                        break;
                }
            }
            else
            {
                window.RequestStop ();
            }
        }
    }

    /// <summary>Returns the path to <c>~/.tui/clet.config.json</c>.</summary>
    internal static string GetConfigPath ()
    {
        if (!OperatingSystem.IsWindows ()
            && Environment.GetEnvironmentVariable ("HOME") is { Length: > 0 } homeOverride)
        {
            return Path.Combine (homeOverride, ".tui", ConfigFileName);
        }

        string home = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);

        return Path.Combine (home, ".tui", ConfigFileName);
    }

    /// <summary>Creates the config file with annotated defaults if it doesn't already exist.</summary>
    internal static void EnsureConfigFile (string configPath)
    {
        if (File.Exists (configPath))
        {
            return;
        }

        string? dir = Path.GetDirectoryName (configPath);

        if (dir is not null && !Directory.Exists (dir))
        {
            Directory.CreateDirectory (dir);
        }

        File.WriteAllText (configPath, DefaultConfigContent);
    }

    /// <summary>
    /// Validates the config by checking JSON syntax, then reloading and re-applying
    /// the MEC-based configuration. Returns an error message if something is wrong,
    /// or null if valid. Bad sources never poison global state — the configuration
    /// builder skips them and falls back to library defaults on its own.
    /// </summary>
    internal static string? ValidateConfig (string configPath)
    {
        if (!File.Exists (configPath))
        {
            return null;
        }

        try
        {
            ParseJsonc (File.ReadAllText (configPath));
        }
        catch (JsonException ex)
        {
            int line = ex.LineNumber.HasValue ? (int)ex.LineNumber.Value + 1 : 0;
            int col = ex.BytePositionInLine.HasValue ? (int)ex.BytePositionInLine.Value + 1 : 0;

            string details = ex.Message;
            int pathIdx = details.IndexOf (" Path:", StringComparison.Ordinal);

            if (pathIdx > 0)
            {
                details = details[..pathIdx];
            }

            return $"{details}\n\nLine {line}, Column {col}";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        // Unknown themes are silently ignored by the MEC-based loader (2.5+),
        // so check the theme name explicitly to keep the pre-2.5 UX of
        // surfacing a bad "Theme" value to the user.
        string? themeError = CheckUnknownTheme (File.ReadAllText (configPath));

        if (themeError is not null)
        {
            return themeError;
        }

        // Syntax is valid — reload and re-apply; per-source errors are collected
        // in TuiJsonErrors instead of thrown.
        CletConfiguration.Reload ();

        return ConsumeConfigErrors ();
    }

    /// <summary>
    /// Returns an error message when the config <paramref name="text"/> selects a
    /// theme that is not in the theme catalog, or null when the theme is valid or
    /// absent. The MEC-based loader ignores unknown theme names silently.
    /// </summary>
    private static string? CheckUnknownTheme (string text)
    {
        string? theme = null;

        try
        {
            JsonNode? root = JsonNode.Parse (
                text,
                documentOptions: new ()
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });

            if (root is JsonObject obj && obj["Theme"] is JsonValue value)
            {
                value.TryGetValue (out theme);
            }
        }
        catch (JsonException)
        {
            // Syntax problems are reported by the caller's syntax check.
            return null;
        }

        if (string.IsNullOrEmpty (theme))
        {
            return null;
        }

        ImmutableList<string> themeNames = ThemeManager.GetThemeNames ();

        if (themeNames.Any (n => string.Equals (n, theme, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return $"Unknown theme \"{theme}\". Available themes: {string.Join (", ", themeNames)}.";
    }

    /// <summary>
    /// Parses <paramref name="text"/> as JSONC (comments and trailing commas allowed)
    /// purely for syntax validation. Throws <see cref="JsonException"/> with
    /// line/column info on failure.
    /// </summary>
    private static void ParseJsonc (string text)
    {
        using JsonDocument _ = JsonDocument.Parse (
            text,
            new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
    }

    /// <summary>
    /// Drains <see cref="TuiJsonErrors"/> (logging each entry) and returns the
    /// collected messages as a single string, or null when there were none.
    /// </summary>
    private static string? ConsumeConfigErrors ()
    {
        IReadOnlyList<string> errors = TuiJsonErrors.GetErrors ();

        // Print logs the errors via Logging and clears the list.
        TuiJsonErrors.Print ();

        return errors.Count > 0 ? string.Join ("\n", errors) : null;
    }

    /// <summary>
    /// Annotated default config content that explains common settings.
    /// JSON with comments (JSONC) — Terminal.Gui's configuration loader supports // comments.
    /// Settings use the nested shape required by Terminal.Gui 2.5+ (dotted
    /// top-level keys are treated as legacy and skipped).
    /// </summary>
    internal const string DefaultConfigContent =
        """
        {
          // ═══════════════════════════════════════════════════════════════════════
          //  clet configuration — ~/.tui/clet.config.json
          //
          //  This file configures Terminal.Gui settings for the `clet` tool.
          //  Terminal.Gui loads this automatically (nested settings shape, 2.5+).
          //
          //  Edit and save (Ctrl+S) to apply changes live.
          //  See: https://tui-cs.github.io/Terminal.Gui/docs/config.html
          //  Schema: https://tui-cs.github.io/Terminal.Gui/schemas/tui-config-schema.json
          // ═══════════════════════════════════════════════════════════════════════

          "$schema": "https://tui-cs.github.io/Terminal.Gui/schemas/tui-config-schema.json",

          // ─── General Settings ─────────────────────────────────────────────────

          // Separator character for key bindings displayed in the UI (e.g. "Ctrl+S")
          // "Key": { "Separator": "+" },

          // Set Force16Colors to true to force 16-color mode (useful for minimal
          // terminal emulators)
          // "Driver": { "Force16Colors": false },

          // Set IsMouseDisabled to true to disable mouse support entirely
          // "Application": { "IsMouseDisabled": false },

          // ─── Key Bindings ─────────────────────────────────────────────────────
          //
          // Key bindings can be customized per-view or globally. Common examples:
          //
          //   "PopoverMenu": { "DefaultKey": "Shift+F10" },
          //
          // Key names follow the pattern: Ctrl+<key>, Alt+<key>, Shift+<key>, F1–F12
          // Multiple modifiers: "Ctrl+Shift+S"
          //
          // See the schema reference for the full list of bindable commands.

          // "PopoverMenu": { "DefaultKey": "Shift+F10" },

          // ─── Themes ───────────────────────────────────────────────────────────
          //
          // Terminal.Gui ships with a Default theme (based on terminal colors) and
          // supports custom themes. Each theme defines color schemes for different
          // UI elements: Base, Dialog, Menu, Error, and Accent.
          //
          // Built-in themes:
          //   "Default", "Dark", "Light", "Anders", "TurboPascal 5",
          //   "Green Phosphor", "Amber Phosphor", "8-Bit"
          //
          // Set the active theme:
          // "Theme": "Anders",
          //
          // A color scheme has these states:
          //   Normal    — default appearance
          //   Focus     — when the view has keyboard focus
          //   HotNormal — hot-key character in normal state
          //   HotFocus  — hot-key character when focused
          //   Disabled  — when the view is disabled
          //
          // Color values can be:
          //   Named:  "Black", "Blue", "Green", "Cyan", "Red", "Magenta",
          //           "Yellow", "White", "BrightBlue", "BrightGreen", etc.
          //   RGB:    "#FF8800" (hex), "rgb(255,136,0)"
          //
          // Themes and Schemes are nested objects keyed by name (Terminal.Gui 2.5+).
          // Example custom theme (uncomment and modify):

          // "Themes": {
          //   "MyCustomTheme": {
          //     "Schemes": {
          //       "Base": {
          //         "Normal": {
          //           "Foreground": "White",
          //           "Background": "DarkBlue"
          //         },
          //         "Focus": {
          //           "Foreground": "BrightYellow",
          //           "Background": "Blue"
          //         },
          //         "HotNormal": {
          //           "Foreground": "BrightCyan",
          //           "Background": "DarkBlue"
          //         },
          //         "HotFocus": {
          //           "Foreground": "BrightCyan",
          //           "Background": "Blue"
          //         },
          //         "Disabled": {
          //           "Foreground": "DarkGray",
          //           "Background": "DarkBlue"
          //         }
          //       },
          //       "Dialog": {
          //         "Normal": {
          //           "Foreground": "Black",
          //           "Background": "LightGray"
          //         }
          //       },
          //       "Menu": {
          //         "Normal": {
          //           "Foreground": "White",
          //           "Background": "DarkCyan"
          //         }
          //       },
          //       "Error": {
          //         "Normal": {
          //           "Foreground": "BrightRed",
          //           "Background": "Black"
          //         }
          //       }
          //     }
          //   }
          // }

          // ─── Tracing (for debugging) ──────────────────────────────────────────
          //
          // Enable trace categories to debug Terminal.Gui internals.
          // Useful values: "Lifecycle", "Drawing", "Layout", "Mouse", "Keyboard"
          //
          // "Trace": { "EnabledCategories": "Lifecycle" }

          // ─── File-Access Allow List ───────────────────────────────────────────
          //
          // Directories (or individual files) that clet edit and clet md are
          // always allowed to open, regardless of the current working directory.
          // Equivalent to VS Code's trusted-folders list.
          //
          // Paths are matched as prefixes, so adding a directory allows all
          // files under it. Size and binary checks still apply.
          //
          // This is the persistent alternative to passing --allow-file each time.
          //
          // Example — allow your projects tree and a shared docs directory:
          // "FileAccessSettings": {
          //   "AllowedPaths": [
          //     "/home/user/projects",
          //     "/home/user/docs"
          //   ]
          // }
        }
        """;
}
