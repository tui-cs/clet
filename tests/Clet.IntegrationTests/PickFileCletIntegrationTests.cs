using System.Text.Json.Nodes;
using Terminal.Gui.App;
using Xunit;
using Terminal.Gui.Cli;

namespace Clet.IntegrationTests;

public class PickFileCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        PickFileClet clet = new ();
        CommandRunOptions options = new ();

        using CancellationTokenSource cts = new ();
        await cts.CancelAsync ();

        CommandResult<JsonNode?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CommandStatus.Cancelled, result.Status);
        Assert.Null (result.Value);
    }

    [Fact (Skip = "FileDialog enumerates mount points during init, crashes under ansi driver. Covered by smoke tests at v0.3.")]
    public async Task RunAsync_WithStopAfterFirstIteration_CompletesWithoutError ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        PickFileClet clet = new ();
        CommandRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CommandResult<JsonNode?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.True (result.Status == CommandStatus.Ok || result.Status == CommandStatus.Cancelled);
    }
}

