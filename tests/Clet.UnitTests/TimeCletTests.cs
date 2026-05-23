using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UnitTests;

public class TimeCletTests
{
    [Fact]
    public void PrimaryAlias_IsTime ()
    {
        TimeClet clet = new ();

        Assert.Equal ("time", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        TimeClet clet = new ();

        Assert.Equal (CommandKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsString ()
    {
        TimeClet clet = new ();

        Assert.Equal (typeof (string), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        TimeClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsTime ()
    {
        TimeClet clet = new ();

        Assert.Contains ("time", clet.Aliases);
    }

    [Fact]
    public void Options_IsEmpty ()
    {
        TimeClet clet = new ();

        Assert.Empty (clet.Options);
    }

    [Fact]
    public void AcceptsPositionalArgs_IsFalse ()
    {
        ICliCommand clet = new TimeClet ();

        Assert.False (clet.AcceptsPositionalArgs);
    }
}
