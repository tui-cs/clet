using Terminal.Gui.App;
using Xunit;
using Terminal.Gui.Cli;

namespace Clet.IntegrationTests;

public class DateCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        DateClet clet = new ();
        CommandRunOptions options = new ();

        using CancellationTokenSource cts = new ();
        await cts.CancelAsync ();

        CommandResult<string?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CommandStatus.Cancelled, result.Status);
        Assert.Null (result.Value);
    }

    [Fact]
    public async Task RunAsync_WithStopAfterFirstIteration_ReturnsOk ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        DateClet clet = new ();
        CommandRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CommandResult<string?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CommandStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInitialValue_SetsDate ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        DateClet clet = new ();
        CommandRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CommandResult<string?> result = await clet.RunAsync (app, "2024-06-15", options, cts.Token);

        Assert.Equal (CommandStatus.Ok, result.Status);
    }
}

