using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UnitTests;

public class LinearRangeCletTests
{
    [Fact]
    public void Metadata_AdvertisesAlias ()
    {
        LinearRangeClet clet = new ();

        Assert.Equal ("linear-range", clet.PrimaryAlias);
        Assert.Contains ("linear-range", clet.Aliases);
        Assert.Equal (CommandKind.Input, clet.Kind);
        Assert.Equal (typeof (System.Text.Json.Nodes.JsonObject), clet.ResultType);
    }

    [Fact]
    public void Options_IncludesModeAndOptions ()
    {
        LinearRangeClet clet = new ();

        Assert.Contains (clet.Options, o => o.Name == "mode");
        Assert.Contains (clet.Options, o => o.Name == "options");
        Assert.Contains (clet.Options, o => o.Name == "orientation");
        Assert.Contains (clet.Options, o => o.Name == "range-kind");
        Assert.Contains (clet.Options, o => o.Name == "allow-empty");
        Assert.Contains (clet.Options, o => o.Name == "hide-legends");
    }

    [Fact]
    public void AcceptsPositionalArgs_IsTrue ()
    {
        LinearRangeClet clet = new ();

        Assert.True (clet.AcceptsPositionalArgs);
    }

    [Fact]
    public async Task RunAsync_NoOptions_ReturnsValidationError ()
    {
        LinearRangeClet clet = new ();
        CommandRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CommandResult<System.Text.Json.Nodes.JsonObject?> result = await clet.RunAsync (
            null!, null, options, cts.Token);

        Assert.Equal (CommandStatus.Error, result.Status);
        Assert.Equal ("validation", result.ErrorCode);
        Assert.Contains ("requires --options", result.ErrorMessage ?? "");
    }

    [Fact]
    public async Task RunAsync_PreCancelled_ReturnsCancelled ()
    {
        LinearRangeClet clet = new ();
        CommandRunOptions options = new ()
        {
            CommandOptions = new Dictionary<string, string> { ["options"] = "a,b,c" },
        };

        using CancellationTokenSource cts = new ();
        await cts.CancelAsync ();

        CommandResult<System.Text.Json.Nodes.JsonObject?> result = await clet.RunAsync (
            null!, null, options, cts.Token);

        Assert.Equal (CommandStatus.Cancelled, result.Status);
    }
}

