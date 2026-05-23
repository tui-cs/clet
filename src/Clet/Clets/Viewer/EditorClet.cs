using System.Collections.ObjectModel;
using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.Configuration;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Terminal.Gui.Drawing;
using Terminal.Gui.Editor;
using Terminal.Gui.Highlighting;
using Terminal.Gui.Input;
using Terminal.Gui.Resources;
using Terminal.Gui.Text.Indentation;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Command = Terminal.Gui.Input.Command;

// ReSharper disable AccessToModifiedClosure

namespace Clet;

internal sealed class EditorClet : IViewerCommand
{
    // Match ted: small files load fully before first paint; larger files stream after the UI appears.
    private const long SynchronousLoadMaxBytes = 1024 * 1024;

    private const long StreamingStatusInterval = 256 * 1024;
    private const int StreamingStatusMilliseconds = 100;

    public string PrimaryAlias => "edit";
    public IReadOnlyList<string> Aliases => ["edit", "editor"];
    public string Description => "Edit text files with menus, undo/redo, find/replace, and glob support.";
    public CommandKind Kind => CommandKind.Viewer;
    public Type ResultType => typeof (void);
    public bool AcceptsPositionalArgs => true;

    public IReadOnlyList<CommandOptionDescriptor> Options =>
    [
        new ("readonly", "r", typeof (bool),
            "Open the file in read-only mode.",
            false, "false")
    ];

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

        // --- Expand positional args (glob patterns + explicit paths) ---

        List<string> files = [];
        string cwd = Directory.GetCurrentDirectory ();
        IReadOnlyList<string> args = options.Arguments;
        string? pendingDeniedPath = null; // set when access is denied; drives dialog

        IReadOnlyList<string> allowedFiles = options.GetExtensionList ("allow-file");

        FileAccessPolicy BuildPolicy (IReadOnlyList<string>? extraAllowed = null)
        {
            IReadOnlyList<string>? merged = FileAccessPolicy.MergeWithConfigPaths (allowedFiles.Count > 0 ? allowedFiles : null);

            if (extraAllowed is { Count: > 0 })
            {
                merged = merged is { Count: > 0 }
                    ? [.. merged, .. extraAllowed]
                    : [.. extraAllowed];
            }

            return new (cwd, merged, options.HasExtension ("allow-binary"), true);
        }

        if (args.Count > 0)
        {
            files = MarkdownContentResolver.ExpandFiles (args, BuildPolicy (), out string? policyError);

            if (policyError is not null)
            {
                // Identify the first non-glob argument that was denied so the
                // dialog can show it and offer to allow it.
                pendingDeniedPath = args
                    .FirstOrDefault (a => !a.Contains ('*') && !a.Contains ('?')) is { } first
                    ? Path.GetFullPath (first)
                    : null;

                // Don't return an error yet — show the interactive dialog in
                // window.Initialized (inside the running event loop).
            }
            else
            {
                foreach (string arg in args)
                {
                    if (arg.Contains ('*') || arg.Contains ('?'))
                    {
                        continue;
                    }

                    string fullPath = Path.GetFullPath (arg);

                    if (!files.Contains (fullPath))
                    {
                        files.Add (fullPath);
                    }
                }
            }
        }

        string? filePath = files.Count > 0 ? files[0] : null;
        // When access was denied, use the intended path for the window title.
        string? fileName = (filePath ?? pendingDeniedPath) is { } fp ? Path.GetFileName (fp) : null;
        string? lastDirectory = filePath is not null ? Path.GetDirectoryName (filePath) : null;
        string? savedText;
        bool accessDialogCancelled = false;

        bool readOnly = options.CommandOptions.TryGetValue ("readonly", out string? roVal)
                        && roVal is "true" or "1";

        // --- Build the UI ---

        Runnable window = new ()
        {
            Title = fileName ?? "Untitled",
            Width = Dim.Fill (),
            Height = Dim.Fill (),
            BorderStyle = LineStyle.None
        };

        // --- Settings are loaded by ConfigurationManager via [ConfigurationProperty] ---

        Editor editor = new ()
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill (),
            Height = Dim.Fill (1),
            ReadOnly = readOnly,
            ConvertTabsToSpaces = EditorSettings.ConvertTabsToSpaces,
            IndentationSize = EditorSettings.IndentSize,
            WordWrap = EditorSettings.WordWrap,
            ShowTabs = EditorSettings.ShowTabs,
            CompletionProvider = EditorSettings.AutoComplete ? new WordCompletionProvider () : null,
            ViewportSettings = EditorSettings.Scrollbars
                ? ViewportSettingsFlags.HasScrollBars
                : ViewportSettingsFlags.None
        };

        // Apply gutter options from settings
        GutterOptions initGutter = GutterOptions.None;

        if (EditorSettings.LineNumbers)
        {
            initGutter |= GutterOptions.LineNumbers;
        }

        if (EditorSettings.FoldIndicators)
        {
            initGutter |= GutterOptions.Folding;
        }

        editor.GutterOptions = initGutter;

        if (EditorSettings.AutoIndent)
        {
            editor.IndentationStrategy = new DefaultIndentationStrategy ();
        }

        editor.HighlightingDefinition = filePath is not null
            ? HighlightingManager.Instance.GetDefinitionByExtension (Path.GetExtension (filePath))
            : null;

        // --- Folding support ---

        BraceFoldingStrategy braceFoldingStrategy = new ();

        void InstallFolding ()
        {
            if (editor.Document is null)
            {
                return;
            }

            FoldingManager fm = new (editor.Document);
            braceFoldingStrategy.UpdateFoldings (fm, editor.Document);
            editor.FoldingManager = fm;

            editor.Document.Changed += (_, _) =>
            {
                if (editor.FoldingManager is not null && editor.Document is not null)
                {
                    braceFoldingStrategy.UpdateFoldings (editor.FoldingManager, editor.Document);
                }
            };
        }

        // --- Markdown preview ---

        Markdown? markdownPreview = null;
        bool syncingScroll = false;
        bool isMarkdownFile = filePath is not null
                              && Path.GetExtension (filePath).Equals (".md", StringComparison.OrdinalIgnoreCase);

        // View-menu toggle item — declared early so preview state helpers can reference it.
        MenuItem previewMarkdownItem = new () { Title = "  _Preview Markdown", Enabled = isMarkdownFile };

        void OnPreviewViewportChanged (object? sender, DrawEventArgs e)
        {
            if (markdownPreview is null || syncingScroll)
            {
                return;
            }

            syncingScroll = true;

            try
            {
                int previewContentHeight = markdownPreview.GetContentSize ().Height;
                int previewViewportHeight = markdownPreview.Viewport.Height;
                int maxPreviewY = Math.Max (0, previewContentHeight - previewViewportHeight);
                int previewY = markdownPreview.Viewport.Y;

                int editorContentHeight = editor.GetContentSize ().Height;
                int editorViewportHeight = editor.Viewport.Height;
                int maxEditorY = Math.Max (0, editorContentHeight - editorViewportHeight);

                int newY = maxPreviewY > 0
                    ? (int)((long)previewY * maxEditorY / maxPreviewY)
                    : 0;

                editor.Viewport = editor.Viewport with { Y = Math.Clamp (newY, 0, maxEditorY) };
            }
            finally
            {
                syncingScroll = false;
            }
        }

        void OnDocumentChangedForPreview (object? sender, EventArgs e)
        {
            if (markdownPreview is null)
            {
                return;
            }

            markdownPreview.Text = editor.Document?.Text ?? string.Empty;
        }

        void ShowMarkdownPreview ()
        {
            if (markdownPreview is not null)
            {
                return;
            }

            markdownPreview = new Markdown
            {
                X = Pos.Right (editor),
                Y = editor.Y,
                Width = Dim.Fill (),
                Height = editor.Height,
                Text = editor.Document?.Text ?? string.Empty,
                ViewportSettings = ViewportSettingsFlags.HasScrollBars,
                SyntaxHighlighter = new TextMateSyntaxHighlighter ()
            };

            editor.Width = Dim.Percent (50);
            window.Add (markdownPreview);

            // Sync scrolling bidirectionally.
            editor.ViewportChanged += OnEditorViewportChanged;
            markdownPreview.ViewportChanged += OnPreviewViewportChanged;

            // Update preview when document content changes.
            if (editor.Document is not null)
            {
                editor.Document.Changed += OnDocumentChangedForPreview;
            }
        }

        void HideMarkdownPreview ()
        {
            if (markdownPreview is null)
            {
                return;
            }

            editor.ViewportChanged -= OnEditorViewportChanged;
            markdownPreview.ViewportChanged -= OnPreviewViewportChanged;

            if (editor.Document is not null)
            {
                editor.Document.Changed -= OnDocumentChangedForPreview;
            }

            window.Remove (markdownPreview);
            markdownPreview.Dispose ();
            markdownPreview = null;

            editor.Width = Dim.Fill ();
        }

        void RefreshPreviewDocument ()
        {
            if (markdownPreview is null)
            {
                return;
            }

            if (editor.Document is not null)
            {
                editor.Document.Changed -= OnDocumentChangedForPreview;
                editor.Document.Changed += OnDocumentChangedForPreview;
            }

            markdownPreview.Text = editor.Document?.Text ?? string.Empty;
        }

        void ToggleMarkdownPreview ()
        {
            if (previewMarkdownItem.Title.StartsWith ("✓"))
            {
                HideMarkdownPreview ();
                previewMarkdownItem.Title = "  _Preview Markdown";
            }
            else
            {
                ShowMarkdownPreview ();
                previewMarkdownItem.Title = "✓ _Preview Markdown";
            }
        }

        void UpdatePreviewEnabled ()
        {
            previewMarkdownItem.Enabled = isMarkdownFile;

            if (!isMarkdownFile && markdownPreview is not null)
            {
                HideMarkdownPreview ();
                previewMarkdownItem.Title = "  _Preview Markdown";
            }
            else if (isMarkdownFile && markdownPreview is not null)
            {
                RefreshPreviewDocument ();
            }
        }

        // --- Shared editor chrome ---

        EditorMenuBar menu = new (editor);
        EditorStatusBar statusBar = new (editor);
        string initialTheme = ThemeManager.Theme;

        if (string.IsNullOrWhiteSpace (initialTheme))
        {
            initialTheme = ThemeManager.GetThemeNames ().FirstOrDefault () ?? "Theme";
        }

        statusBar.ThemeDropDown.Width = 12;
        statusBar.ThemeDropDown.Height = 1;
        statusBar.ThemeDropDown.Text = initialTheme;
        SpinnerView loadStatusSpinner = statusBar.LoadStatusSpinner;
        Shortcut loadStatusShortcut = statusBar.LoadSpinnerShortcut;
        long? lastFileByteSize = null;
        string lastStatusVerb = "Loaded";
        object streamingStatusLock = new ();
        long lastStreamingStatusUnits;
        DateTime lastStreamingStatusUpdate;
        long streamingStatusOperationId = 0;
        CancellationTokenSource? progressiveLoadCts = null;

        List<string> fileSelectorFiles = [];
        ObservableCollection<string> fileSelectorDisplayNames = [];
        bool switchingFileSelector = false;
        int fileMenuItemsWidth = 32;
        int filenameShortcutWidth = 12;
        int lastFilenameMenuWidth = -1;
        DropDownList filenameDropDown = new ()
        {
            Source = new ListWrapper<string> (fileSelectorDisplayNames),
            ReadOnly = true,
            Text = fileName ?? "<untitled>",
            CanFocus = false,
            Width = filenameShortcutWidth
        };

        // --- Local state helpers ---

        bool UnsavedChanges ()
        {
            return editor.Document?.UndoStack.IsOriginalFile == false;
        }

        void UpdateModifiedIndicator ()
        {
            bool dirty = UnsavedChanges ();
            window.Title = dirty ? $"{fileName ?? "Untitled"}*" : fileName ?? "Untitled";
        }

        static string GetFileDisplayName (string? path)
        {
            if (path is null)
            {
                return "<untitled>";
            }

            string fullPath = Path.GetFullPath (path);

            return string.IsNullOrEmpty (fullPath) ? path : fullPath;
        }

        int GetFilenameTextColumns ()
        {
            return Math.Max (1, filenameShortcutWidth - 3);
        }

        string GetFittedFileDisplayName (string? path)
        {
            return EditorFileDisplay.FitPath (GetFileDisplayName (path), GetFilenameTextColumns ());
        }

        void UpdateFileSelectorText ()
        {
            switchingFileSelector = true;
            filenameDropDown.Text = GetFittedFileDisplayName (filePath);
            switchingFileSelector = false;
        }

        void EnsureFileInSelector (string fullPath)
        {
            if (fileSelectorFiles.Contains (fullPath, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            fileSelectorFiles.Add (fullPath);
            fileSelectorDisplayNames.Add (GetFittedFileDisplayName (fullPath));
        }

        void RefreshFileSelectorDisplayNames ()
        {
            switchingFileSelector = true;
            fileSelectorDisplayNames.Clear ();

            foreach (string displayName in EditorFileDisplay.FitPaths (fileSelectorFiles.Select (GetFileDisplayName),
                         GetFilenameTextColumns ()))
            {
                fileSelectorDisplayNames.Add (displayName);
            }

            if (fileSelectorFiles.Count == 0)
            {
                fileSelectorDisplayNames.Add (GetFittedFileDisplayName (null));
            }

            filenameDropDown.Text = GetFittedFileDisplayName (filePath);
            switchingFileSelector = false;
        }

        void RebuildFileSelectorItems ()
        {
            fileSelectorFiles.Clear ();
            fileSelectorDisplayNames.Clear ();

            foreach (string file in files)
            {
                string fullPath = Path.GetFullPath (file);

                if (!fileSelectorFiles.Contains (fullPath, StringComparer.OrdinalIgnoreCase))
                {
                    fileSelectorFiles.Add (fullPath);
                }
            }

            if (filePath is not null)
            {
                EnsureFileInSelector (filePath);
            }

            if (fileSelectorDisplayNames.Count == 0)
            {
                fileSelectorDisplayNames.Add (GetFittedFileDisplayName (null));
            }

            RefreshFileSelectorDisplayNames ();
            UpdateFileSelectorText ();
        }

        void UpdateFilenameShortcutLayout ()
        {
            const int shortcutChromeColumns = 3;
            const int gapBeforeFilenameShortcut = 1;
            const int minimumShortcutWidth = 12;
            int menuWidth = menu.Frame.Width;

            if (menuWidth <= 0)
            {
                return;
            }

            int longestDisplayName = Math.Max (
                GetFileDisplayName (filePath).Length,
                fileSelectorFiles.Count == 0
                    ? "<untitled>".Length
                    : fileSelectorFiles.Max (f => GetFileDisplayName (f).Length));
            int desiredWidth = longestDisplayName + shortcutChromeColumns;
            int availableWidth = menuWidth - fileMenuItemsWidth - gapBeforeFilenameShortcut;
            int maximumNaturalWidth = Math.Max (minimumShortcutWidth, menuWidth / 2);
            int width = Math.Max (1, Math.Min (desiredWidth, Math.Min (availableWidth, maximumNaturalWidth)));

            if (width == filenameShortcutWidth)
            {
                return;
            }

            filenameShortcutWidth = width;
            filenameDropDown.Width = filenameShortcutWidth;
            RefreshFileSelectorDisplayNames ();
            filenameDropDown.SetNeedsDraw ();
        }

        void UpdateSyntaxLanguage (string path)
        {
            editor.HighlightingDefinition =
                HighlightingManager.Instance.GetDefinitionByExtension (Path.GetExtension (path));
            statusBar.UpdateLanguageShortcut ();
        }

        void SetIdleLoadStatus (string status)
        {
            loadStatusSpinner.Visible = false;
            loadStatusSpinner.AutoSpin = false;
            loadStatusShortcut.Title = status;
            loadStatusShortcut.HelpText = status;
            loadStatusSpinner.SetNeedsDraw ();
            loadStatusShortcut.SetNeedsDraw ();
        }

        void UpdateModifiedStatus ()
        {
            if (loadStatusSpinner.AutoSpin)
            {
                return;
            }

            string verb = UnsavedChanges () ? "Modified" : lastStatusVerb;
            SetIdleLoadStatus (FormatCompletedProgress (verb, lastFileByteSize));
        }

        void RefreshDocumentByteSize ()
        {
            TextDocument? document = editor.Document;

            if (document is null)
            {
                lastFileByteSize = null;

                return;
            }

            Encoding encoding = document.Encoding;
            lastFileByteSize = encoding.GetByteCount (document.Text);
        }

        // --- File operations ---

        void ApplyLoadedFileState (string fullPath, long? fileSize)
        {
            filePath = fullPath;
            fileName = Path.GetFileName (fullPath);
            lastDirectory = Path.GetDirectoryName (fullPath);
            lastFileByteSize = fileSize;
            lastStatusVerb = "Loaded";
            savedText = editor.Document?.Text ?? string.Empty;
            editor.ClearSelection ();
            editor.CaretOffset = 0;
            UpdateSyntaxLanguage (fullPath);
            InstallFolding ();
            UpdateModifiedIndicator ();
            EnsureFileInSelector (fullPath);
            UpdateFileSelectorText ();
            UpdateModifiedStatus ();
            isMarkdownFile = Path.GetExtension (fullPath).Equals (".md", StringComparison.OrdinalIgnoreCase);
            UpdatePreviewEnabled ();
            editor.SetFocus ();
        }

        void OpenMissingFile (string fullPath)
        {
            filePath = fullPath;
            fileName = Path.GetFileName (fullPath);
            lastDirectory = Path.GetDirectoryName (fullPath);
            lastFileByteSize = 0;
            lastStatusVerb = "Loaded";
            savedText = string.Empty;
            editor.ClearSelection ();
            editor.Document = new TextDocument ();
            editor.Document.UndoStack.DiscardOriginalFileMarker ();
            editor.CaretOffset = 0;
            UpdateSyntaxLanguage (fullPath);
            InstallFolding ();
            UpdateModifiedIndicator ();
            EnsureFileInSelector (fullPath);
            UpdateFileSelectorText ();
            UpdateModifiedStatus ();
            isMarkdownFile = Path.GetExtension (fullPath).Equals (".md", StringComparison.OrdinalIgnoreCase);
            UpdatePreviewEnabled ();
            editor.SetFocus ();
        }

        void OpenFileSynchronously (string fullPath)
        {
            try
            {
                using FileStream stream = File.OpenRead (fullPath);
                long fileSize = stream.Length;
                editor.ClearSelection ();
                editor.LoadAsync (stream, cancellationToken: cancellationToken).GetAwaiter ().GetResult ();
                ApplyLoadedFileState (fullPath, fileSize);
            }
            catch (OperationCanceledException)
            {
                CompleteAnyStreamingStatus ("Load canceled");
            }
            catch (Exception ex) when (IsFileOperationException (ex))
            {
                CompleteAnyStreamingStatus ("Load failed");
            }
        }

        void LoadFile (string path)
        {
            string fullPath = Path.GetFullPath (path);
            FileInfo file = new (fullPath);

            // Cancel any in-flight progressive load so it cannot overwrite state.
            progressiveLoadCts?.Cancel ();
            progressiveLoadCts = null;

            if (!file.Exists)
            {
                OpenMissingFile (fullPath);

                return;
            }

            if (file.Length <= SynchronousLoadMaxBytes)
            {
                OpenFileSynchronously (fullPath);

                return;
            }

            progressiveLoadCts = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
            CancellationTokenSource currentCts = progressiveLoadCts;
            app.Invoke (() => _ = BeginProgressiveLoadAsync (fullPath, currentCts));
        }

        async Task<bool> BeginProgressiveLoadAsync (string fullPath, CancellationTokenSource loadCts)
        {
            long? statusOperationId = null;
            CancellationToken loadToken = loadCts.Token;

            try
            {
                await using FileStream stream = File.OpenRead (fullPath);
                long fileSize = stream.Length;
                long startedStatusOperationId = BeginStreamingStatus (FormatStartingProgress ("Loading", fileSize));
                statusOperationId = startedStatusOperationId;

                IProgress<TextDocumentProgress> progress =
                    CreateStreamingProgress (progress => ReportLoadProgress (startedStatusOperationId, progress));

                editor.ClearSelection ();

                await editor.LoadAsync (
                    stream,
                    progress: progress,
                    cancellationToken: loadToken,
                    marshal: action => InvokeOnAppAsync (app, action));

                // If cancelled between LoadAsync completing and here, don't apply state.
                loadToken.ThrowIfCancellationRequested ();

                await InvokeOnAppAsync (app, () =>
                {
                    // Final guard: if another load started while we were awaiting the marshal,
                    // this CTS will have been cancelled — bail out.
                    if (loadToken.IsCancellationRequested)
                    {
                        return;
                    }

                    ApplyLoadedFileState (fullPath, fileSize);
                    CompleteStreamingStatus (
                        startedStatusOperationId,
                        FormatCompletedProgress ("Loaded", fileSize));
                });

                return true;
            }
            catch (OperationCanceledException)
            {
                if (statusOperationId is { } startedStatusOperationId)
                {
                    CompleteStreamingStatus (startedStatusOperationId, "Load canceled");
                }
                else
                {
                    CompleteAnyStreamingStatus ("Load canceled");
                }

                return false;
            }
            catch (Exception ex) when (IsFileOperationException (ex))
            {
                if (statusOperationId is { } startedStatusOperationId)
                {
                    CompleteStreamingStatus (startedStatusOperationId, "Load failed");
                }
                else
                {
                    CompleteAnyStreamingStatus ("Load failed");
                }

                return false;
            }
        }

        void ReportLoadProgress (long statusOperationId, TextDocumentProgress progress)
        {
            if (!ShouldReportStreamingProgress (statusOperationId, progress))
            {
                return;
            }

            SetLoadStatus (FormatProgress ("Loading", progress), true, statusOperationId);
        }

        IProgress<TextDocumentProgress> CreateStreamingProgress (Action<TextDocumentProgress> handler)
        {
            return new Progress<TextDocumentProgress> (handler);
        }

        long BeginStreamingStatus (string status)
        {
            long statusOperationId = Interlocked.Increment (ref streamingStatusOperationId);
            ResetStreamingStatusThrottle ();
            SetLoadStatus (status, true, statusOperationId);

            return statusOperationId;
        }

        void CompleteStreamingStatus (long statusOperationId, string status)
        {
            long completionOperationId = statusOperationId + 1;

            if (Interlocked.CompareExchange (
                    ref streamingStatusOperationId,
                    completionOperationId,
                    statusOperationId)
                != statusOperationId)
            {
                return;
            }

            SetLoadStatus (status, false, completionOperationId);
        }

        void CompleteAnyStreamingStatus (string status)
        {
            long completionOperationId = Interlocked.Increment (ref streamingStatusOperationId);
            SetLoadStatus (status, false, completionOperationId);
        }

        void SetLoadStatus (string status, bool showSpinner, long statusOperationId)
        {
            void Update ()
            {
                if (Interlocked.Read (ref streamingStatusOperationId) != statusOperationId)
                {
                    return;
                }

                loadStatusSpinner.Visible = showSpinner;
                loadStatusSpinner.AutoSpin = showSpinner;
                loadStatusShortcut.Title = status;
                loadStatusShortcut.HelpText = status;
                loadStatusSpinner.SetNeedsDraw ();
                loadStatusShortcut.SetNeedsDraw ();
            }

            app.Invoke (Update);
        }

        void ResetStreamingStatusThrottle ()
        {
            lock (streamingStatusLock)
            {
                lastStreamingStatusUpdate = DateTime.MinValue;
                lastStreamingStatusUnits = 0;
            }
        }

        bool ShouldReportStreamingProgress (long statusOperationId, TextDocumentProgress progress)
        {
            if (Interlocked.Read (ref streamingStatusOperationId) != statusOperationId)
            {
                return false;
            }

            long processedUnits = progress.BytesProcessed ?? progress.CharactersProcessed;
            long? totalUnits = progress.TotalBytes ?? progress.TotalCharacters;

            if (totalUnits == processedUnits)
            {
                return true;
            }

            lock (streamingStatusLock)
            {
                DateTime now = DateTime.UtcNow;

                if (processedUnits - lastStreamingStatusUnits < StreamingStatusInterval
                    && now - lastStreamingStatusUpdate < TimeSpan.FromMilliseconds (StreamingStatusMilliseconds))
                {
                    return false;
                }

                lastStreamingStatusUnits = processedUnits;
                lastStreamingStatusUpdate = now;
            }

            return true;
        }

        static string FormatProgress (string verb, TextDocumentProgress progress)
        {
            string processed = progress.BytesProcessed is { } bytesProcessed
                ? FormatByteCount (bytesProcessed)
                : $"{progress.CharactersProcessed:N0} chars";

            string? total = progress.TotalBytes is { } totalBytes
                ? FormatByteCount (totalBytes)
                : progress.TotalCharacters is { } totalCharacters
                    ? $"{totalCharacters:N0} chars"
                    : null;

            if (total is null)
            {
                return $"{verb} {processed}";
            }

            if (progress.Fraction is { } fraction)
            {
                return $"{verb} {processed} of {total} ({fraction:P0})";
            }

            return $"{verb} {processed} of {total}";
        }

        static string FormatStartingProgress (string verb, long totalBytes)
        {
            return $"{verb} 0 B of {FormatByteCount (totalBytes)}";
        }

        static string FormatCompletedProgress (string verb, long? totalBytes)
        {
            return totalBytes is { } bytes
                ? $"{verb} {FormatByteCount (bytes)}"
                : verb;
        }

        static string FormatByteCount (long bytes)
        {
            string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
            double value = bytes;
            int unitIndex = 0;

            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }

            string format = unitIndex == 0 ? "N0" : "N1";

            return $"{value.ToString (format)} {units[unitIndex]}";
        }

        static bool IsFileOperationException (Exception ex)
        {
            return ex is IOException or UnauthorizedAccessException;
        }

        bool SaveFile ()
        {
            if (filePath is null)
            {
                return SaveAs ();
            }

            try
            {
                File.WriteAllText (filePath, editor.Document?.Text ?? string.Empty);
                savedText = editor.Document?.Text ?? string.Empty;
                lastFileByteSize = Encoding.UTF8.GetByteCount (savedText);
                lastStatusVerb = "Saved";
                editor.Document?.UndoStack.MarkAsOriginalFile ();
                UpdateModifiedIndicator ();
                UpdateModifiedStatus ();
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery (app, "Error", ex.Message, Terminal.Gui.Resources.Strings.btnOk);

                return false;
            }

            return true;
        }

        bool SaveAs ()
        {
            string? path = ShowSaveAsDialogPath ();

            return path is not null && SaveFileAs (path);
        }

        string? ShowSaveAsDialogPath ()
        {
            SaveDialog sd = new ();

            if (lastDirectory is not null)
            {
                sd.Path = lastDirectory;
            }

            if (fileName is not null)
            {
                sd.Path = Path.Combine (sd.Path ?? ".", fileName);
            }

            app.Run (sd);
            bool canceled = sd.Canceled;
            string path = sd.Path;
            sd.Dispose ();

            if (canceled || string.IsNullOrWhiteSpace (path))
            {
                return null;
            }

            return path;
        }

        bool SaveFileAs (string path)
        {
            filePath = Path.GetFullPath (path);
            fileName = Path.GetFileName (filePath);
            lastDirectory = Path.GetDirectoryName (filePath);
            EnsureFileInSelector (filePath);
            UpdateFileSelectorText ();

            return SaveFile ();
        }

        bool PromptSaveIfDirty ()
        {
            if (!UnsavedChanges ())
            {
                return true;
            }

            int? result = MessageBox.Query (
                app,
                "Unsaved Changes",
                $"Save changes to {fileName ?? "Untitled"}?",
                Strings.btnCancel, Strings.btnNo, Strings.btnYes);

            return result switch
            {
                null or 0 => false,
                1 => true,
                2 => SaveFile (),
                _ => true
            };
        }

        string? ShowOpenDialog ()
        {
            if (!PromptSaveIfDirty ())
            {
                return null;
            }

            OpenDialog od = new ()
            {
                Title = "Open",
                AllowsMultipleSelection = false,
                AllowedTypes = [new AllowedTypeAny ()],
                MustExist = true,
                OpenMode = OpenMode.File
            };

            if (lastDirectory is not null)
            {
                od.Path = lastDirectory;
            }

            app.Run (od);

            string? selectedPath = null;

            if (od is { Canceled: false, FilePaths.Count: > 0 })
            {
                selectedPath = od.FilePaths[0];
                lastDirectory = Path.GetDirectoryName (Path.GetFullPath (selectedPath));
            }

            od.Dispose ();

            return selectedPath;
        }

        void OpenFile ()
        {
            string? selectedPath = ShowOpenDialog ();

            if (selectedPath is not null)
            {
                LoadFile (selectedPath);
            }
        }

        void QuitEditor ()
        {
            if (!PromptSaveIfDirty ())
            {
                return;
            }

            window.RequestStop ();
        }

        // --- Find/Replace ---

        void ShowFindReplace (bool showReplace = false)
        {
            FindReplaceDialog dlg = new (editor, showReplace);
            app.Run (dlg);
            dlg.Dispose ();
        }

        // --- About dialog ---

        void ShowAbout ()
        {
            string editorVersion = VersionInfo.GetAssemblyVersion (
                typeof (Editor).Assembly, "unknown");

            Dialog about = new ()
            {
                Title = "About clet edit",
                Width = Dim.Percent (50),
                Height = 12
            };

            Label info = new ()
            {
                X = 1,
                Y = 0,
                Width = Dim.Fill (1),
                Text = $"""
                        clet {VersionInfo.GetCletVersion ()}
                        Terminal.Gui {VersionInfo.GetTerminalGuiVersion ()}
                        Terminal.Gui.Editor {editorVersion}

                        https://github.com/gui-cs/clet
                        """
            };

            Button ok = new () { Text = "OK", X = Pos.Center (), Y = Pos.Bottom (info) + 1, IsDefault = true };
            ok.Accepting += (_, _) => about.RequestStop ();
            about.Add (info, ok);
            app.Run (about);
            about.Dispose ();
        }

        // --- Settings dialog ---

        void ShowSettings ()
        {
            EditorSettingsDialog dlg = new (editor);
            app.Run (dlg);

            if (dlg.WasAccepted)
            {
                dlg.ApplyTo (editor);
                menu.SyncCheckboxes ();
                SaveViewSettings ();
            }

            dlg.Dispose ();
        }

        void SaveViewSettings ()
        {
            EditorSettings.LineNumbers = editor.GutterOptions.HasFlag (GutterOptions.LineNumbers);
            EditorSettings.FoldIndicators = editor.GutterOptions.HasFlag (GutterOptions.Folding);
            EditorSettings.WordWrap = editor.WordWrap;
            EditorSettings.ShowTabs = editor.ShowTabs;
            EditorSettings.Scrollbars = editor.ViewportSettings.HasFlag (ViewportSettingsFlags.HasScrollBars);
            EditorSettings.IndentSize = editor.IndentationSize;
            EditorSettings.ConvertTabsToSpaces = editor.ConvertTabsToSpaces;
            EditorSettings.AutoIndent = editor.IndentationStrategy is not null;
            EditorSettings.AutoComplete = editor.CompletionProvider is not null;
            EditorSettings.Save ();
        }

        previewMarkdownItem.Action = () =>
        {
            if (isMarkdownFile)
            {
                ToggleMarkdownPreview ();
            }
        };

        menu.ShowOpenDialog = ShowOpenDialog;
        menu.ShowSaveDialog = ShowSaveAsDialogPath;
        menu.NewRequested += (_, _) => NewFile ();
        menu.OpenRequested += (_, e) => LoadFile (e.FilePath);
        menu.SaveRequested += (_, _) => SaveFile ();
        menu.SaveAsRequested += (_, e) => SaveFileAs (e.FilePath);
        menu.QuitRequested += (_, _) => QuitEditor ();
        menu.ViewSettingsChanged += (_, _) => SaveViewSettings ();
        menu.ViewMenu.PopoverMenu!.Root!.Add (new Line (), previewMarkdownItem);

        menu.Add (new MenuBarItem ("_Options",
        [
            new MenuItem { Title = "_Settings...", Action = ShowSettings }
        ]));

        menu.Add (new MenuBarItem ("_Help",
        [
            new MenuItem { Title = "_About", Action = ShowAbout }
        ]));
        fileMenuItemsWidth = Math.Max (fileMenuItemsWidth, menu.GetWidthRequiredForSubViews ());
        app.Iteration += (_, _) =>
        {
            int menuWidth = menu.Frame.Width;
            if (menuWidth == lastFilenameMenuWidth)
            {
                return;
            }

            lastFilenameMenuWidth = menuWidth;
            UpdateFilenameShortcutLayout ();
        };

        filenameDropDown.ValueChanged += (_, _) =>
        {
            if (switchingFileSelector)
            {
                return;
            }

            int index = fileSelectorDisplayNames.IndexOf (filenameDropDown.Text);

            if (index < 0 || index >= fileSelectorFiles.Count)
            {
                UpdateFileSelectorText ();

                return;
            }

            string selectedPath = fileSelectorFiles[index];

            if (filePath is not null && string.Equals (filePath, selectedPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!PromptSaveIfDirty ())
            {
                UpdateFileSelectorText ();

                return;
            }

            LoadFile (selectedPath);
        };
        RebuildFileSelectorItems ();
        UpdateFilenameShortcutLayout ();
        Shortcut filenameShortcut = new ()
        {
            CommandView = filenameDropDown,
            MouseHighlightStates = MouseState.None,
            SchemeName = SchemeManager.SchemesToSchemeName (Schemes.Dialog)
        };
        menu.Add (filenameShortcut);
        UpdateFilenameShortcutLayout ();

        // --- Wire find/replace events ---

        editor.FindRequested += (_, _) => ShowFindReplace ();
        editor.ReplaceRequested += (_, _) => ShowFindReplace (true);

        // --- Wire events ---

        editor.CaretChanged += (_, _) =>
        {
            UpdateModifiedIndicator ();
            UpdateModifiedStatus ();
        };
        editor.ContentChanged += (_, _) =>
        {
            RefreshDocumentByteSize ();
            UpdateModifiedStatus ();
        };

        // --- StatusBar ---

        statusBar.AlignmentModes = AlignmentModes.StartToEnd | AlignmentModes.IgnoreFirstOrLast;
        statusBar.RemoveAll ();
        statusBar.Add (
            statusBar.LanguageShortcut,
            statusBar.ThemeDropDown,
            loadStatusShortcut,
            statusBar.OverwriteShortcut,
            new Shortcut (Application.GetDefaultKey (Command.Quit), Terminal.Gui.Resources.Strings.cmdQuit, QuitEditor),
            new Shortcut (Key.F2, Terminal.Gui.Resources.Strings.cmdOpen, OpenFile),
            new Shortcut (Key.F3, Terminal.Gui.Resources.Strings.cmdSave, () => SaveFile ()),
            statusBar.LocShortcut);

        // --- Assemble window ---

        window.Add (menu, editor, statusBar);

        // --- Load content after layout ---

        window.Initialized += (_, _) =>
        {
            // ── File-access dialog ───────────────────────────────────────────
            if (pendingDeniedPath is not null)
            {
                string dir = Path.GetDirectoryName (pendingDeniedPath) is { Length: > 0 } d
                    ? d
                    : pendingDeniedPath;

                int? choice = MessageBox.Query (
                    app,
                    "File Access Required",
                    $"'{Path.GetFileName (pendingDeniedPath)}' is outside the allowed\n"
                    + $"directories.\n\n{pendingDeniedPath}\n\n"
                    + "How would you like to proceed?",
                    Strings.btnCancel, "_Allow once", "_Add to config");

                switch (choice)
                {
                    case 0 or null:
                        accessDialogCancelled = true;
                        window.RequestStop ();
                        break;

                    case 1: // Allow once — add dir to the session policy only
                        {
                            files = MarkdownContentResolver.ExpandFiles (args, BuildPolicy ([dir]), out _);

                            if (files.Count > 0)
                            {
                                filePath = files[0];
                                fileName = Path.GetFileName (filePath);
                                lastDirectory = Path.GetDirectoryName (filePath);
                                window.Title = fileName;
                                RebuildFileSelectorItems ();
                            }

                            break;
                        }

                    case 2: // Add to config — persist the directory and allow now
                        {
                            FileAccessSettings.AddToConfig (dir);
                            files = MarkdownContentResolver.ExpandFiles (args, BuildPolicy (), out _);

                            if (files.Count > 0)
                            {
                                filePath = files[0];
                                fileName = Path.GetFileName (filePath);
                                lastDirectory = Path.GetDirectoryName (filePath);
                                window.Title = fileName;
                                RebuildFileSelectorItems ();
                            }

                            break;
                        }

                    default:
                        return;
                }
            }

            // ── Normal (or post-allow) content load ──────────────────────────
            if (filePath is not null)
            {
                LoadFile (filePath);
            }
            else if (initial is not null)
            {
                editor.Document = new TextDocument (initial);
                lastFileByteSize = Encoding.UTF8.GetByteCount (initial);
                lastStatusVerb = "Loaded";
                savedText = string.Empty;
                InstallFolding ();
                UpdateModifiedIndicator ();
                UpdateModifiedStatus ();
            }

            statusBar.UpdateLanguageShortcut ();
            editor.SetFocus ();
        };

        // --- Run ---

        try
        {
            await app.RunAsync (window, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new (CommandStatus.Cancelled, null, null, null);
        }

        if (cancellationToken.IsCancellationRequested || accessDialogCancelled)
        {
            return new (CommandStatus.Cancelled, null, null, null);
        }

        return new (CommandStatus.Ok, null, null, null);

        void OnEditorViewportChanged (object? sender, DrawEventArgs e)
        {
            if (markdownPreview is null || syncingScroll)
            {
                return;
            }

            syncingScroll = true;

            try
            {
                int editorContentHeight = editor.GetContentSize ().Height;
                int editorViewportHeight = editor.Viewport.Height;
                int maxEditorY = Math.Max (0, editorContentHeight - editorViewportHeight);
                int editorY = editor.Viewport.Y;

                int previewContentHeight = markdownPreview.GetContentSize ().Height;
                int previewViewportHeight = markdownPreview.Viewport.Height;
                int maxPreviewY = Math.Max (0, previewContentHeight - previewViewportHeight);

                int newY = maxEditorY > 0
                    ? (int)((long)editorY * maxPreviewY / maxEditorY)
                    : 0;

                markdownPreview.Viewport = markdownPreview.Viewport with { Y = Math.Clamp (newY, 0, maxPreviewY) };
            }
            finally
            {
                syncingScroll = false;
            }
        }

        void NewFile ()
        {
            if (!PromptSaveIfDirty ())
            {
                return;
            }

            filePath = null;
            fileName = null;
            lastFileByteSize = 0;
            lastStatusVerb = "Loaded";
            savedText = string.Empty;
            editor.ClearSelection ();
            editor.Document = new TextDocument ();
            editor.CaretOffset = 0;
            editor.HighlightingDefinition = null;
            InstallFolding ();
            UpdateModifiedIndicator ();
            statusBar.UpdateLanguageShortcut ();
            UpdateFileSelectorText ();
            UpdateModifiedStatus ();
            isMarkdownFile = false;
            UpdatePreviewEnabled ();
        }
    }

    private static Task InvokeOnAppAsync (IApplication app, Action action)
    {
        TaskCompletionSource completion = new ();
        app.Invoke (() =>
        {
            try
            {
                action ();
                completion.SetResult ();
            }
            catch (Exception ex)
            {
                completion.SetException (ex);
            }
        });

        return completion.Task;
    }
}
