using Xunit;

namespace Clet.SmokeTests;

// v0.11 smoke matrix per issue #9: process-level invocation of the clet binary.
//
// Five of the six smoke cases land here. They use Process.Start and assert on
// exit code + stdout/stderr. They do not synthesize keystrokes.
//
// The sixth case (`clet select --json` happy-path with an Enter keystroke) is
// deferred to v0.3 when TUIcast is wired up — at v0.11 we have one input clet
// (select), and standing up a PTY/keystroke harness for a single happy-path
// case is not worth the dependency. v0.3 brings 13 more clets and TUIcast at
// the same time per spec §6.3.
public class CletSmokeTests
{
    [Fact]
    public async Task Version_PrintsVersionAndExitsZero ()
    {
        (int exit, string stdout, string stderr) = await CletProcess.RunAsync (["--version"]);

        Assert.Equal (0, exit);
        Assert.Empty (stderr);
        Assert.Matches (@"^clet \d+\.\d+\.\d+(-\S+)?\s*$", stdout);
    }

    [Fact]
    public async Task Help_PrintsUsageAndExitsZero ()
    {
        (int exit, string stdout, string stderr) = await CletProcess.RunAsync (["--help"]);

        Assert.Equal (0, exit);
        Assert.Empty (stderr);
        Assert.Contains ("clet", stdout);
        Assert.Contains ("select", stdout);
    }

    [Fact]
    public async Task OpenCli_EmitsManifestAndExitsZero ()
    {
        (int exit, string stdout, string stderr) = await CletProcess.RunAsync (["--opencli"]);

        Assert.Equal (0, exit);
        Assert.Empty (stderr);
        string trimmed = stdout.TrimEnd ();
        Assert.Contains ("\"name\":\"clet\"", trimmed);
        Assert.Contains ("\"alias\":\"select\"", trimmed);
    }

    // `clet help <alias>` and `clet help` now route through the interactive md viewer
    // (PR #76). The viewer doesn't exit without a keystroke, so it can't be exercised
    // by Process.Start-style smoke tests — that needs the TUIcast keystroke harness
    // deferred to v0.3 per spec §6.3. The successful-dispatch path is covered by the
    // CommandLineRootTests.HelpAlias_KnownAlias_DispatchesHelpViewer unit test (which
    // uses a pre-cancelled token to short-circuit the viewer). The unknown-alias path
    // still smoke-tests cleanly because it returns UsageError *before* dispatching.

    [Fact]
    public async Task UnknownCommand_ExitsWithUsageError ()
    {
        (int exit, _, string stderr) = await CletProcess.RunAsync (["nope"]);

        Assert.Equal (2, exit);
        Assert.Contains ("Unknown command", stderr);
    }

    [Fact]
    public async Task OpenCli_IncludesMdViewer ()
    {
        (int exit, string stdout, string stderr) = await CletProcess.RunAsync (["--opencli"]);

        Assert.Equal (0, exit);
        Assert.Empty (stderr);
        Assert.Contains ("md", stdout);
    }

    [Fact (Skip = "Requires real TG run loop with cancellation; v0.3 TUIcast harness will drive this against the AOT'd binary.")]
    public Task SelectTimeout_EmitsCancelEnvelopeAndExits130 ()
    {
        // Intentionally not implemented at v0.11. The cancellation contract is unit-tested in
        // ExitCodesTests + OutputFormatterTests; a process-level test of the timeout path needs
        // the TUIcast harness from v0.3 to drive the binary in a controlled environment.
        return Task.CompletedTask;
    }

    [Fact]
    public async Task OversizedInitial_ExitsWithValidationError ()
    {
        // Windows has a ~32K command-line length limit; the 64K --initial arg exceeds it.
        // The logic is covered by the unit test (Alias_InitialExceeds64KiB_ExitsWithValidationError).
        if (OperatingSystem.IsWindows ())
        {
            return;
        }

        string oversized = new ('x', 64 * 1024 + 1);

        (int exit, _, _) = await CletProcess.RunAsync (
            ["select", "--json", "--initial", oversized]);

        Assert.Equal (2, exit);
    }

    [Fact]
    public async Task MdOversizedStdin_ExitsWithError ()
    {
        string oversized = new ('x', 8 * 1024 * 1024 + 1);

        (int exit, string stdout, _) = await CletProcess.RunAsync (
            ["md", "--json"], stdin: oversized);

        Assert.Equal (65, exit);
        Assert.Contains ("input-too-large", stdout);
        Assert.Contains ("\"status\":\"error\"", stdout);
    }
}
