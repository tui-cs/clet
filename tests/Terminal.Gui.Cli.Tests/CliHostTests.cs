using Xunit;

namespace Terminal.Gui.Cli.Tests;

public class CliHostTests
{
    [Fact]
    public async Task RunAsync_NoArgs_ReturnsOkAndWritesHelp ()
    {
        using StringWriter stdout = new ();
        using StringWriter stderr = new ();

        CliHost host = new (o => o.ApplicationName = "testapp");

        int exitCode = await host.RunAsync ([], TestContext.Current.CancellationToken, stdout, stderr);

        Assert.Equal (ExitCodes.Ok, exitCode);
        Assert.Contains ("Commands:", stdout.ToString ());
    }

    [Fact]
    public async Task RunAsync_Version_WritesVersionString ()
    {
        using StringWriter stdout = new ();
        using StringWriter stderr = new ();

        CliHost host = new (o =>
        {
            o.ApplicationName = "testapp";
            o.Version = "1.2.3";
        });

        int exitCode = await host.RunAsync (["--version"], TestContext.Current.CancellationToken, stdout, stderr);

        Assert.Equal (ExitCodes.Ok, exitCode);
        Assert.Contains ("testapp 1.2.3", stdout.ToString ());
    }

    [Fact]
    public async Task RunAsync_UnknownCommand_ReturnsUsageError ()
    {
        using StringWriter stdout = new ();
        using StringWriter stderr = new ();

        CliHost host = new (o => o.ApplicationName = "testapp");

        int exitCode = await host.RunAsync (["nope"], TestContext.Current.CancellationToken, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exitCode);
        Assert.Contains ("unknown command", stderr.ToString ());
    }

    [Fact]
    public async Task RunAsync_OpenCli_WritesJson ()
    {
        using StringWriter stdout = new ();
        using StringWriter stderr = new ();

        CliHost host = new (o =>
        {
            o.ApplicationName = "testapp";
            o.Version = "0.1.0";
        });
        host.Registry.Register (new StubCommand ("demo", ["demo", "d"]));

        int exitCode = await host.RunAsync (["--opencli"], TestContext.Current.CancellationToken, stdout, stderr);

        Assert.Equal (ExitCodes.Ok, exitCode);
        string output = stdout.ToString ();
        Assert.Contains ("\"opencli\":\"0.1\"", output);
        Assert.Contains ("\"demo\"", output);
    }

    [Fact]
    public async Task RunAsync_CommandHelp_ReturnsOk ()
    {
        using StringWriter stdout = new ();
        using StringWriter stderr = new ();

        CliHost host = new (o => o.ApplicationName = "testapp");
        host.Registry.Register (new StubCommand ("demo", ["demo"]));

        int exitCode = await host.RunAsync (["demo", "--help"], TestContext.Current.CancellationToken, stdout, stderr);

        Assert.Equal (ExitCodes.Ok, exitCode);
        Assert.Contains ("Stub command: demo", stdout.ToString ());
    }

    [Fact]
    public async Task RunAsync_InvalidOption_ReturnsUsageError ()
    {
        using StringWriter stdout = new ();
        using StringWriter stderr = new ();

        CliHost host = new (o => o.ApplicationName = "testapp");
        host.Registry.Register (new StubCommand ("demo", ["demo"]));

        int exitCode = await host.RunAsync (["demo", "--unknown", "val"], TestContext.Current.CancellationToken, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exitCode);
        Assert.Contains ("unknown option", stderr.ToString ());
    }

    [Fact]
    public async Task RunAsync_ConsumerGlobalOptions_FlowToExtensions ()
    {
        using StringWriter stdout = new ();
        using StringWriter stderr = new ();

        ExtensionCapturingCommand cmd = new ();

        CliHost host = new (o =>
        {
            o.ApplicationName = "testapp";
            o.GlobalOptions.Add (new ("allow-file", null, "Permit file access", IsFlag: false, Repeatable: true));
            o.GlobalOptions.Add (new ("no-browse", null, "Disable browsing", IsFlag: true));
        });
        host.Registry.Register (cmd);

        // This test exercises parsing only — TUI dispatch would hang without a terminal.
        // We verify the parse succeeds and options flow correctly by checking that the
        // command receives them (command captures options in RunAsync before throwing).
        int exitCode = await host.RunAsync (
            ["capture", "--allow-file", "/tmp", "--no-browse", "--json"],
            TestContext.Current.CancellationToken, stdout, stderr);

        // The command will return Ok and we can verify from its captured options
        Assert.Equal (ExitCodes.Ok, exitCode);
        Assert.True (cmd.CapturedOptions!.HasExtension ("no-browse"));
        Assert.Equal ("/tmp", cmd.CapturedOptions!.GetExtensionList ("allow-file")[0]);
    }
}

/// <summary>Command that captures its options for test verification (no TUI).</summary>
internal sealed class ExtensionCapturingCommand : ICliCommand
{
    public string PrimaryAlias => "capture";
    public IReadOnlyList<string> Aliases => ["capture"];
    public string Description => "Captures options for testing";
    public CommandKind Kind => CommandKind.Input;
    public Type ResultType => typeof (string);
    public IReadOnlyList<CommandOptionDescriptor> Options => [];

    public CommandRunOptions? CapturedOptions { get; private set; }

    public Task<CommandResult> RunAsync (
        Terminal.Gui.App.IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken)
    {
        CapturedOptions = options;

        return Task.FromResult (new CommandResult (CommandStatus.Ok, "captured", null, null));
    }
}
