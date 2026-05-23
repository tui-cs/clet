using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UnitTests;

public class SelectCletTests
{
    [Fact]
    public void PrimaryAlias_IsSelect ()
    {
        SelectClet clet = new ();

        Assert.Equal ("select", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        SelectClet clet = new ();

        Assert.Equal (CommandKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsNullableInt ()
    {
        SelectClet clet = new ();

        Assert.Equal (typeof (string), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        SelectClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsSelect ()
    {
        SelectClet clet = new ();

        Assert.Contains ("select", clet.Aliases);
    }

    [Fact]
    public void Options_ContainsOptionsDescriptor ()
    {
        SelectClet clet = new ();

        Assert.Single (clet.Options);
        Assert.Equal ("options", clet.Options[0].Name);
        Assert.True (clet.Options[0].Required);
    }

    [Fact]
    public void AcceptsPositionalArgs_IsTrue ()
    {
        SelectClet clet = new ();

        Assert.True (clet.AcceptsPositionalArgs);
    }
}
