using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UnitTests;

public class PickFileCletTests
{
    [Fact]
    public void PrimaryAlias_IsPickFile ()
    {
        PickFileClet clet = new ();

        Assert.Equal ("pick-file", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        PickFileClet clet = new ();

        Assert.Equal (CommandKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsJsonNode ()
    {
        PickFileClet clet = new ();

        Assert.Equal (typeof (System.Text.Json.Nodes.JsonNode), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        PickFileClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsPickFile ()
    {
        PickFileClet clet = new ();

        Assert.Contains ("pick-file", clet.Aliases);
    }

    [Fact]
    public void Options_ContainsMultiRootFilter ()
    {
        PickFileClet clet = new ();

        Assert.Equal (3, clet.Options.Count);
        Assert.Equal ("multi", clet.Options[0].Name);
        Assert.Equal ("root", clet.Options[1].Name);
        Assert.Equal ("filter", clet.Options[2].Name);
        Assert.False (clet.Options[0].Required);
    }

    [Fact]
    public void AcceptsPositionalArgs_IsFalse ()
    {
        ICliCommand clet = new PickFileClet ();

        Assert.False (clet.AcceptsPositionalArgs);
    }
}
