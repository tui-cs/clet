using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UnitTests;

public class MultiSelectCletTests
{
    [Fact]
    public void PrimaryAlias_IsMultiSelect ()
    {
        MultiSelectClet clet = new ();

        Assert.Equal ("multi-select", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        MultiSelectClet clet = new ();

        Assert.Equal (CommandKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsJsonArray ()
    {
        MultiSelectClet clet = new ();

        Assert.Equal (typeof (System.Text.Json.Nodes.JsonArray), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        MultiSelectClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsMultiSelect ()
    {
        MultiSelectClet clet = new ();

        Assert.Contains ("multi-select", clet.Aliases);
    }

    [Fact]
    public void Options_ContainsOptionsDescriptor ()
    {
        MultiSelectClet clet = new ();

        Assert.Single (clet.Options);
        Assert.Equal ("options", clet.Options[0].Name);
        Assert.True (clet.Options[0].Required);
    }

    [Fact]
    public void AcceptsPositionalArgs_IsTrue ()
    {
        MultiSelectClet clet = new ();

        Assert.True (clet.AcceptsPositionalArgs);
    }
}
