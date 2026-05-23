using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Drawing;
using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UITests;

/// <summary>
///     In-process UI render harness for clets.
///     See <c>tests/SPEC.md</c> §2.3 and §3.2 for design.
/// </summary>
/// <remarks>
///     The harness runs a clet on the test thread, captures text and ANSI snapshots during
///     <see cref="IApplication.Iteration"/>, then requests stop after a few draw cycles so rendering
///     assertions are deterministic without a background loop.
/// </remarks>
internal sealed class CletUiHarness<T> : IAsyncDisposable
{
    private readonly IApplication _app;
    private readonly CancellationTokenSource _cts;
    private readonly Task<CommandResult<T>> _cletTask;
    private readonly string? _initialAnsiSnapshot;
    private readonly string? _initialTextSnapshot;

    private CletUiHarness (
        IApplication app,
        CancellationTokenSource cts,
        Task<CommandResult<T>> cletTask,
        string? initialAnsiSnapshot,
        string? initialTextSnapshot)
    {
        _app = app;
        _cts = cts;
        _cletTask = cletTask;
        _initialAnsiSnapshot = initialAnsiSnapshot;
        _initialTextSnapshot = initialTextSnapshot;
    }

    /// <summary>Start a harness for the given input clet. Returns after the initial render has been captured.</summary>
    public static Task<CletUiHarness<T>> StartAsync (
        ICliCommand<T> clet,
        string? initial = null,
        CommandRunOptions? options = null,
        int width = 60,
        int height = 10)
        => StartCoreAsync (
            (app, ct) => clet.RunAsync (app, initial, options ?? new CommandRunOptions (), ct),
            width, height);

    /// <summary>Start a harness for a viewer clet. Result.Value is always default(T) for viewers.</summary>
    public static Task<CletUiHarness<T>> StartViewerAsync (
        IViewerCommand viewer,
        string? initial = null,
        CommandRunOptions? options = null,
        int width = 60,
        int height = 15)
        => StartCoreAsync (
            async (app, ct) =>
            {
                CommandResult result = await viewer.RunAsync (app, initial, options ?? new CommandRunOptions (), ct);

                return new CommandResult<T> (result.Status, default, result.ErrorCode, result.ErrorMessage);
            },
            width, height);

    private static async Task<CletUiHarness<T>> StartCoreAsync (
        Func<IApplication, CancellationToken, Task<CommandResult<T>>> run,
        int width,
        int height)
    {
        // Always FullScreen in the harness — even for input clets that ship Inline in
        // production. AppModel is process-global; leaving it implicit makes snapshots
        // order-dependent between tests, and Inline mode renders into a small region
        // below the cursor that doesn't fill the screen size we set, so snapshots come
        // out mostly empty. FullScreen gives the full Driver.Contents grid for
        // deterministic assertions. Tests that specifically need to verify Inline
        // rendering behavior would need a separate harness mode (not on the v0.5 list).
        Application.AppModel = AppModel.FullScreen;

        // Note: not setting `DisableRealDriverIO=1` here, even though MarkdownHelpRenderer
        // does for the print-mode help pipeline. The interactive Markdown View under TG
        // currently regresses with that flag set. We achieve test determinism instead by
        // explicitly pinning the screen size via SetScreenSize below — that's the property
        // the reviewer's suggestion was after (deterministic in headless CI). Revisit if
        // we ever see CI flakes that trace back to driver capability probes.

        IApplication app = Application.Create ();
        app.Init (DriverRegistry.Names.ANSI);
        app.Driver?.SetScreenSize (width, height);

        CancellationTokenSource cts = new ();
        string? ansiSnapshot = null;
        string? textSnapshot = null;
        int iterations = 0;
        int previousHash = 0;
        bool sawNonEmpty = false;
        int stableCount = 0;
        const int stableThreshold = 2;
        const int maxIterations = 50;
        TextWriter originalOut = Console.Out;
        StringWriter capturedOut = new ();
        EventHandler<EventArgs<IApplication?>> handler = (_, _) =>
        {
            iterations++;

            // Capture latest snapshot every iteration so we always have the most recent.
            ansiSnapshot = CanonicalizeAnsi (app.Driver?.ToAnsi ());
            textSnapshot = BuildTextSnapshot (app.Driver?.Contents);

            // Wait until contents are *stable* across consecutive iterations before stopping.
            // "Stable" = same hash for two iterations in a row, after at least one non-empty
            // frame. Views that do async filesystem population or post additional layout work
            // (file picker, Markdown/editor viewers) need more than a fixed count of iterations.
            (int hash, bool nonEmpty) = HashContents (app.Driver?.Contents);

            if (!sawNonEmpty)
            {
                if (nonEmpty)
                {
                    sawNonEmpty = true;
                    previousHash = hash;
                    stableCount = 1;
                }
                return;
            }

            if (hash == previousHash)
            {
                stableCount++;
                if (stableCount >= stableThreshold)
                {
                    app.RequestStop ();
                }
            }
            else
            {
                previousHash = hash;
                stableCount = 1;
            }

            // Hard cap to prevent infinite loops if content never stabilizes (e.g. cursor blink).
            if (iterations >= maxIterations)
            {
                app.RequestStop ();
            }
        };

        app.Iteration += handler;
        Task<CommandResult<T>> task;
        Console.SetOut (capturedOut);

        try
        {
            task = run (app, cts.Token);
            await task;

            string capturedAnsi = CanonicalizeAnsi (capturedOut.ToString ());
            if (!string.IsNullOrEmpty (capturedAnsi))
            {
                ansiSnapshot = capturedAnsi;
            }
        }
        finally
        {
            Console.SetOut (originalOut);
            app.Iteration -= handler;
            await capturedOut.DisposeAsync ();
        }

        return new (app, cts, task, ansiSnapshot, textSnapshot);
    }

    /// <summary>
    ///     Snapshot the current screen as plain text, one line per row, trailing whitespace trimmed
    ///     per row. Source: <c>app.Driver.Contents</c> (pre-clipping).
    /// </summary>
    public string SnapshotText ()
    {
        if (_initialTextSnapshot is not null)
        {
            return _initialTextSnapshot;
        }

        return BuildTextSnapshot (_app.Driver?.Contents);
    }

    private static string BuildTextSnapshot (Cell[,]? contents)
    {
        if (contents is null)
        {
            return string.Empty;
        }

        int rows = contents.GetLength (0);
        int cols = contents.GetLength (1);
        StringBuilder sb = new (rows * (cols + 1));

        for (int r = 0; r < rows; r++)
        {
            int lineStart = sb.Length;
            for (int c = 0; c < cols; c++)
            {
                string g = contents[r, c].Grapheme;
                sb.Append (string.IsNullOrEmpty (g) ? " " : g);
            }

            // Trim trailing whitespace from this row.
            int end = sb.Length;
            while (end > lineStart && char.IsWhiteSpace (sb[end - 1]))
            {
                end--;
            }
            sb.Length = end;
            sb.Append ('\n');
        }

        return sb.ToString ();
    }

    /// <summary>
    ///     Returns a content-hash for the cell grid plus a flag indicating whether the grid
    ///     has any non-space glyphs. Used by the startup-stability detector.
    /// </summary>
    private static (int Hash, bool NonEmpty) HashContents (Cell[,]? contents)
    {
        if (contents is null)
        {
            return (0, false);
        }

        int rows = contents.GetLength (0);
        int cols = contents.GetLength (1);
        int hash = 17;
        bool nonEmpty = false;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                string g = contents[r, c].Grapheme;
                hash = unchecked(hash * 31 + (string.IsNullOrEmpty (g) ? 0 : g.GetHashCode ()));

                if (!string.IsNullOrEmpty (g) && g != " ")
                {
                    nonEmpty = true;
                }
            }
        }

        return (hash, nonEmpty);
    }

    /// <summary>Snapshot the screen as ANSI, including styling.</summary>
    public string SnapshotAnsi ()
    {
        return _initialAnsiSnapshot ?? CanonicalizeAnsi (_app.Driver?.ToAnsi ());
    }

    /// <summary>
    ///     Compare the current ANSI snapshot against a stored golden file under
    ///     <c>tests/Clet.UITests/Goldens/&lt;name&gt;</c>. Set <c>CLET_REGEN_GOLDENS=1</c> or
    ///     <c>UPDATE_SNAPSHOTS=1</c> to rewrite mismatched goldens in place.
    /// </summary>
    public void AssertMatchesAnsiGolden (string fileName)
    {
        string actual = NormalizeForGolden (SnapshotAnsi ());
        string path = ResolveGoldenPath (fileName);
        bool regen = ShouldRegenerateGoldens ();

        if (!File.Exists (path))
        {
            if (regen)
            {
                WriteAnsiGolden (path, actual);
                Assert.Fail ($"ANSI golden created at {path}. Re-run without regeneration enabled to verify.");
            }

            Assert.Fail ($"ANSI golden not found: {path}. Run with CLET_REGEN_GOLDENS=1 to create it.");
        }

        string expected = NormalizeForGolden (CanonicalizeAnsi (File.ReadAllText (path)));

        if (expected == actual)
        {
            return;
        }

        string actualPath = path + ".actual";
        WriteAnsiGolden (actualPath, actual);

        if (regen)
        {
            WriteAnsiGolden (path, actual);
            Assert.Fail ($"ANSI golden updated at {path}. Re-run without regeneration enabled to verify.");
        }

        Assert.Fail ($"""
            ANSI golden '{fileName}' does not match {path}.

            Plain-text render of the actual screen:
            ----------------------------------------------------------------------
            {SnapshotText ()}
            ----------------------------------------------------------------------

            Exact look (with colors/styles): cat '{actualPath}'
            Expected look:                   cat '{path}'
            """);
    }

    private static string ResolveGoldenPath (string fileName)
    {
        // For reads, the bin directory works (CopyToOutputDirectory ships goldens with the
        // test binary). But for `CLET_REGEN_GOLDENS=1` writes, we must target the source tree
        // so the regenerated file actually shows up in `git status`. The csproj bakes the
        // source path in via `<AssemblyMetadata Include="GoldensSourcePath" />` and we read
        // it back with AssemblyMetadataAttribute.
        bool regen = ShouldRegenerateGoldens ();

        if (regen)
        {
            string? sourcePath = typeof (CletUiHarness<T>).Assembly
                .GetCustomAttributes (typeof (System.Reflection.AssemblyMetadataAttribute), false)
                .Cast<System.Reflection.AssemblyMetadataAttribute> ()
                .FirstOrDefault (a => a.Key == "GoldensSourcePath")
                ?.Value;

            if (!string.IsNullOrEmpty (sourcePath))
            {
                return Path.Combine (sourcePath, fileName);
            }
        }

        string assemblyDir = Path.GetDirectoryName (typeof (CletUiHarness<T>).Assembly.Location)!;
        return Path.Combine (assemblyDir, "Goldens", fileName);
    }

    private static bool ShouldRegenerateGoldens ()
        => Environment.GetEnvironmentVariable ("CLET_REGEN_GOLDENS") == "1"
           || Environment.GetEnvironmentVariable ("UPDATE_SNAPSHOTS") is "1" or "true";

    private static string CanonicalizeAnsi (string? ansi)
        => (ansi ?? string.Empty).Replace ("\r\n", "\n").Replace ("\r", "\n");

    private static string NormalizeForGolden (string ansi)
    {
        string tempPath = Path.GetTempPath ().TrimEnd (Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string escapedTempPath = System.Text.RegularExpressions.Regex.Escape (tempPath);

        ansi = System.Text.RegularExpressions.Regex.Replace (
            ansi,
            $@"{escapedTempPath}[\\/]+clet-ui-[0-9a-fA-F]+",
            "<clet-ui-temp>");

        return System.Text.RegularExpressions.Regex.Replace (
            ansi,
            "clet-ui-[0-9a-fA-F]+",
            "clet-ui-temp");
    }

    private static void WriteAnsiGolden (string path, string ansi)
    {
        Directory.CreateDirectory (Path.GetDirectoryName (path)!);
        File.WriteAllText (path, ansi, new UTF8Encoding (false));
    }

    public async ValueTask DisposeAsync ()
    {
        try
        {
            if (!_cletTask.IsCompleted)
            {
                await _cts.CancelAsync ();

                try
                {
                    await _cletTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected on cancellation. Anything else surfaces — a swallowed
                    // exception from the clet under test would mask real failures.
                }
            }
        }
        finally
        {
            _cts.Dispose ();
            _app.Dispose ();
        }
    }
}

