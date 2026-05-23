using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UITests;

/// <summary>
///     ANSI rendering tests for every built-in clet. Test parallelization is disabled at the
///     assembly level (see <c>AssemblyAttributes.cs</c>) because TG's <c>IApplication</c> has
///     process-global state.
/// </summary>
public class CletUiTests
{
    [Fact]
    public async Task TextClet_InitialRender_MatchesAnsiGolden ()
        => await AssertInputRenderAsync (
            new TextClet (), "text.ans", "Enter text", initial: "hello");

    [Fact]
    public async Task SelectClet_InitialRender_MatchesAnsiGolden ()
        => await AssertInputRenderAsync (
            new SelectClet (), "select.ans", "Select an option", options: Options ("options", "Apple,Banana,Cherry"));

    [Fact]
    public async Task IntClet_InitialRender_MatchesAnsiGolden ()
        => await AssertInputRenderAsync (
            new IntClet (), "int.ans", "Enter a number", initial: "42");

    [Fact]
    public async Task DecimalClet_InitialRender_MatchesAnsiGolden ()
        => await AssertInputRenderAsync (
            new DecimalClet (), "decimal.ans", "Enter a decimal", initial: "12.5");

    [Fact]
    public async Task ConfirmClet_InitialRender_MatchesAnsiGolden ()
        => await AssertInputRenderAsync (
            new ConfirmClet (), "confirm.ans", "Confirm", initial: "yes");

    [Fact]
    public async Task DateClet_InitialRender_MatchesAnsiGolden ()
        => await AssertInputRenderAsync (
            new DateClet (), "date.ans", "Select a date", initial: "2026-05-19");

    [Fact]
    public async Task TimeClet_InitialRender_MatchesAnsiGolden ()
        => await AssertInputRenderAsync (
            new TimeClet (), "time.ans", "Select a time", initial: "12:34:56");

    [Fact]
    public async Task DurationClet_InitialRender_MatchesAnsiGolden ()
        => await AssertInputRenderAsync (
            new DurationClet (), "duration.ans", "Enter a duration", initial: "PT1H30M");

    [Fact]
    public async Task ColorClet_InitialRender_MatchesAnsiGolden ()
        => await AssertInputRenderAsync (
            new ColorClet (), "color.ans", "Pick a color", initial: "#336699", width: 80, height: 18);

    [Fact]
    public async Task MultiSelectClet_InitialRender_MatchesAnsiGolden ()
        => await AssertInputRenderAsync (
            new MultiSelectClet (), "multi-select.ans", "Select one or more options",
            initial: "Apple,Cherry", options: Options ("options", "Apple,Banana,Cherry"));

    [Fact]
    public async Task AttributePickerClet_InitialRender_MatchesAnsiGolden ()
        => await AssertInputRenderAsync (
            new AttributePickerClet (), "attribute-picker.ans", "Pick text attributes", width: 80, height: 20);

    [Fact]
    public async Task PickFileClet_InitialRender_MatchesAnsiGolden ()
        => await WithTempDirectoryAsync (async root =>
        {
            string path = Path.Combine (root, "sample.txt");
            await File.WriteAllTextAsync (path, "sample");
            SetStableTimestamp (path);
            SetStableTimestamp (root);
            await AssertInputRenderAsync (
                new PickFileClet (), "pick-file.ans", "file (Enter",
                options: Options ("root", root), width: 80, height: 20);
        });

    [Fact]
    public async Task PickDirectoryClet_InitialRender_MatchesAnsiGolden ()
        => await WithTempDirectoryAsync (async root =>
        {
            string path = Path.Combine (root, "sample");
            Directory.CreateDirectory (path);
            SetStableTimestamp (path);
            SetStableTimestamp (root);
            await AssertInputRenderAsync (
                new PickDirectoryClet (), "pick-directory.ans", "directory (Enter",
                options: Options ("root", root), width: 80, height: 20);
        });

    [Fact]
    public async Task LinearRangeClet_InitialRender_MatchesAnsiGolden ()
        => await AssertInputRenderAsync (
            new LinearRangeClet (), "linear-range.ans", "Pick one",
            options: Options ("options", "Free,Pro,Team"), width: 80, height: 12);

    [Fact]
    public async Task EditorClet_InitialRender_MatchesAnsiGolden ()
        => await AssertViewerRenderAsync (
            new EditorClet (), "edit.ans", "<untitled>", width: 80, height: 20);

    [Fact]
    public async Task EditorClet_LoadedMarkdownFile_MatchesAnsiGolden ()
        => await WithTempDirectoryAsync (async root =>
        {
            string path = Path.Combine (root, "sample.md");
            await File.WriteAllTextAsync (path, "# Hello\n\nBody\n");

            await using var harness = await CletUiHarness<object?>.StartViewerAsync (
                new EditorClet (),
                options: new CommandRunOptions
                {
                    Arguments = [path],
                    Extensions = new Dictionary<string, IReadOnlyList<string>> { ["allow-file"] = new List<string> { path } },
                },
                width: 120,
                height: 20);

            string snapshot = harness.SnapshotText ();
            Assert.Contains ("MarkDown", snapshot);
            Assert.Contains ("Default ▼", snapshot);
            Assert.Contains ("Loaded 14 B", snapshot);
            Assert.Single (AllIndexesOf (snapshot, "Loaded 14 B"));
            Assert.Contains ("sample.md", snapshot);
            harness.AssertMatchesAnsiGolden ("edit-markdown.ans");
        });

    [Fact]
    public async Task EditorClet_LongFilename_DoesNotOverlapHelpMenu ()
        => await WithTempDirectoryAsync (async root =>
        {
            string filename = "this-is-a-very-long-markdown-file-name.md";
            string path = Path.Combine (root, filename);
            await File.WriteAllTextAsync (path, "# Hello\n");

            await using var harness = await CletUiHarness<object?>.StartViewerAsync (
                new EditorClet (),
                options: new CommandRunOptions
                {
                    Arguments = [path],
                    Extensions = new Dictionary<string, IReadOnlyList<string>> { ["allow-file"] = new List<string> { path } },
                },
                width: 80,
                height: 12);

            string firstLine = harness.SnapshotText ().Split ('\n')[0];
            Assert.Contains ("Help", firstLine);
            Assert.Contains ("…", firstLine);
            Assert.Contains (".md", firstLine);
            Assert.DoesNotContain (path, firstLine);
        });

    [Fact]
    public async Task MarkdownClet_InitialRender_MatchesAnsiGolden ()
        => await AssertViewerRenderAsync (
            new MarkdownClet (), "md.ans", "Hello",
            initial: "# Hello\n\nThis is a test.", width: 80, height: 16);

    [Fact]
    public async Task ConfigClet_InitialRender_MatchesAnsiGolden ()
        => await WithTempHomeAsync (async () => await AssertViewerRenderAsync (
            new ConfigClet (), "config.ans", "clet config", width: 100, height: 20));

    [Fact]
    public async Task HelpClet_InitialRender_MatchesAnsiGolden ()
    {
        CommandRegistry registry = new ();
        BuiltInClets.RegisterAll (registry);

        await AssertViewerRenderAsync (
            new HelpClet (registry), "help.ans", "clet", width: 80, height: 18);
    }

    private static async Task AssertInputRenderAsync<T> (
        ICliCommand<T> clet,
        string goldenName,
        string _,
        string? initial = null,
        CommandRunOptions? options = null,
        int width = 60,
        int height = 10)
    {
        await using var harness = await CletUiHarness<T>.StartAsync (
            clet, initial: initial, options: options, width: width, height: height);

        Assert.False (string.IsNullOrWhiteSpace (harness.SnapshotAnsi ()));
        harness.AssertMatchesAnsiGolden (goldenName);
    }

    private static async Task AssertViewerRenderAsync (
        IViewerCommand clet,
        string goldenName,
        string _,
        string? initial = null,
        CommandRunOptions? options = null,
        int width = 60,
        int height = 15)
    {
        await using var harness = await CletUiHarness<object?>.StartViewerAsync (
            clet, initial: initial, options: options, width: width, height: height);

        Assert.False (string.IsNullOrWhiteSpace (harness.SnapshotAnsi ()));
        harness.AssertMatchesAnsiGolden (goldenName);
    }

    private static CommandRunOptions Options (string name, string value)
        => new () { CommandOptions = new Dictionary<string, string> { [name] = value } };

    private static IEnumerable<int> AllIndexesOf (string text, string value)
    {
        int index = 0;

        while ((index = text.IndexOf (value, index, StringComparison.Ordinal)) >= 0)
        {
            yield return index;
            index += value.Length;
        }
    }

    private static async Task WithTempDirectoryAsync (Func<string, Task> run)
    {
        string root = Path.Combine (Path.GetTempPath (), $"clet-ui-{Guid.NewGuid ():N}");
        Directory.CreateDirectory (root);

        try
        {
            await run (root);
        }
        finally
        {
            Directory.Delete (root, recursive: true);
        }
    }

    private static async Task WithTempHomeAsync (Func<Task> run)
    {
        string root = Path.Combine (Path.GetTempPath (), $"clet-home-{Guid.NewGuid ():N}");
        Directory.CreateDirectory (root);
        string? oldHome = Environment.GetEnvironmentVariable ("HOME");

        try
        {
            Environment.SetEnvironmentVariable ("HOME", root);
            await run ();
        }
        finally
        {
            Environment.SetEnvironmentVariable ("HOME", oldHome);
            Directory.Delete (root, recursive: true);
        }
    }

    private static void SetStableTimestamp (string path)
    {
        DateTime timestamp = new (2026, 1, 2, 3, 4, 5, DateTimeKind.Local);

        if (Directory.Exists (path))
        {
            Directory.SetLastWriteTime (path, timestamp);
            return;
        }

        File.SetLastWriteTime (path, timestamp);
    }
}

