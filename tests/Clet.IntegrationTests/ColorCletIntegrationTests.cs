using Terminal.Gui.App;
using Xunit;
using Terminal.Gui.Cli;

namespace Clet.IntegrationTests;

public class ColorCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        ColorClet clet = new ();
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

        ColorClet clet = new ();
        CommandRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CommandResult<string?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CommandStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInitialValue_SetsColor ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        ColorClet clet = new ();
        CommandRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CommandResult<string?> result = await clet.RunAsync (app, "#ff0000", options, cts.Token);

        Assert.Equal (CommandStatus.Ok, result.Status);
    }
}

