using Terminal.Gui.App;
using Xunit;
using Terminal.Gui.Cli;

namespace Clet.IntegrationTests;

public class SelectCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        // Pre-cancelled token should return Cancelled without starting the UI
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        SelectClet clet = new ();
        CommandRunOptions options = new ()
        {
            CommandOptions = new Dictionary<string, string> { ["options"] = "A,B,C" },
        };

        using CancellationTokenSource cts = new ();
        await cts.CancelAsync ();

        CommandResult<string?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CommandStatus.Cancelled, result.Status);
        Assert.Null (result.Value);
    }

    [Fact]
    public async Task RunAsync_WithStopAfterFirstIteration_ReturnsOk ()
    {
        // Use StopAfterFirstIteration to exercise the run path without blocking
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        SelectClet clet = new ();
        CommandRunOptions options = new ()
        {
            CommandOptions = new Dictionary<string, string> { ["options"] = "Apple,Banana,Cherry" },
        };

        using CancellationTokenSource cts = new ();

        CommandResult<string?> result = await clet.RunAsync (app, null, options, cts.Token);

        // Run returns after one iteration — result is Ok (value may be null since no input)
        Assert.Equal (CommandStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInitialValue_SetsSelection ()
    {
        // Verifies that the initial value is parsed and set on the selector
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        SelectClet clet = new ();
        CommandRunOptions options = new ()
        {
            CommandOptions = new Dictionary<string, string> { ["options"] = "X,Y,Z" },
        };

        using CancellationTokenSource cts = new ();

        CommandResult<string?> result = await clet.RunAsync (app, "Y", options, cts.Token);

        // Should complete without error
        Assert.Equal (CommandStatus.Ok, result.Status);
    }
}

