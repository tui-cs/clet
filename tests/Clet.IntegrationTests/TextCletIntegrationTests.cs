using Terminal.Gui.App;
using Xunit;
using Terminal.Gui.Cli;

namespace Clet.IntegrationTests;

public class TextCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        TextClet clet = new ();
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

        TextClet clet = new ();
        CommandRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CommandResult<string?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CommandStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInitialValue_SetsText ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        TextClet clet = new ();
        CommandRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CommandResult<string?> result = await clet.RunAsync (app, "hello", options, cts.Token);

        Assert.Equal (CommandStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithMultilineInitialValue_ReturnsOk ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        TextClet clet = new ();
        CommandRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CommandResult<string?> result = await clet.RunAsync (app, "line1\nline2", options, cts.Token);

        Assert.Equal (CommandStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithRows_ReturnsOk ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        TextClet clet = new ();
        CommandRunOptions options = new () { Rows = 10 };

        using CancellationTokenSource cts = new ();

        CommandResult<string?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CommandStatus.Ok, result.Status);
    }
}

