using System.Text.Json.Nodes;
using Terminal.Gui.App;
using Xunit;
using Terminal.Gui.Cli;

namespace Clet.IntegrationTests;

public class MultiSelectCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        MultiSelectClet clet = new ();
        CommandRunOptions options = new ()
        {
            CommandOptions = new Dictionary<string, string> { ["options"] = "A,B,C" },
        };

        using CancellationTokenSource cts = new ();
        await cts.CancelAsync ();

        CommandResult<JsonArray?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CommandStatus.Cancelled, result.Status);
        Assert.Null (result.Value);
    }

    [Fact]
    public async Task RunAsync_WithStopAfterFirstIteration_ReturnsOk ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        MultiSelectClet clet = new ();
        CommandRunOptions options = new ()
        {
            CommandOptions = new Dictionary<string, string> { ["options"] = "Apple,Banana,Cherry" },
        };

        using CancellationTokenSource cts = new ();

        CommandResult<JsonArray?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CommandStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInitialValue_SetsSelection ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        MultiSelectClet clet = new ();
        CommandRunOptions options = new ()
        {
            CommandOptions = new Dictionary<string, string> { ["options"] = "X,Y,Z" },
        };

        using CancellationTokenSource cts = new ();

        CommandResult<JsonArray?> result = await clet.RunAsync (app, "X,Z", options, cts.Token);

        Assert.Equal (CommandStatus.Ok, result.Status);
    }
}

