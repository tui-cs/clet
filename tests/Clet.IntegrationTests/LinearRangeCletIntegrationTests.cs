using Terminal.Gui.App;
using Xunit;
using Terminal.Gui.Cli;

namespace Clet.IntegrationTests;

public class LinearRangeCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        LinearRangeClet clet = new ();
        CommandRunOptions options = new ()
        {
            CommandOptions = new Dictionary<string, string> { ["options"] = "A,B,C,D" },
        };

        using CancellationTokenSource cts = new ();
        await cts.CancelAsync ();

        CommandResult<System.Text.Json.Nodes.JsonObject?> result = await clet.RunAsync (
            app, null, options, cts.Token);

        Assert.Equal (CommandStatus.Cancelled, result.Status);
        Assert.Null (result.Value);
    }

    [Fact]
    public async Task RunAsync_SingleMode_WithStopAfterFirstIteration_ReturnsOk ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        LinearRangeClet clet = new ();
        CommandRunOptions options = new ()
        {
            CommandOptions = new Dictionary<string, string>
            {
                ["options"] = "10,20,30,40,50",
                ["mode"] = "single",
            },
        };

        using CancellationTokenSource cts = new ();

        CommandResult<System.Text.Json.Nodes.JsonObject?> result = await clet.RunAsync (
            app, null, options, cts.Token);

        Assert.Equal (CommandStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_RangeMode_WithInitial_StopsCleanly ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        LinearRangeClet clet = new ();
        CommandRunOptions options = new ()
        {
            CommandOptions = new Dictionary<string, string>
            {
                ["options"] = "S,M,L,XL",
                ["mode"] = "range",
                ["range-kind"] = "closed",
            },
        };

        using CancellationTokenSource cts = new ();

        CommandResult<System.Text.Json.Nodes.JsonObject?> result = await clet.RunAsync (
            app, "S..L", options, cts.Token);

        Assert.Equal (CommandStatus.Ok, result.Status);
    }
}

