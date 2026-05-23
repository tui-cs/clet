using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UnitTests;

public class DateCletTests
{
    [Fact]
    public void PrimaryAlias_IsDate ()
    {
        DateClet clet = new ();

        Assert.Equal ("date", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        DateClet clet = new ();

        Assert.Equal (CommandKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsString ()
    {
        DateClet clet = new ();

        Assert.Equal (typeof (string), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        DateClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsDate ()
    {
        DateClet clet = new ();

        Assert.Contains ("date", clet.Aliases);
    }

    [Fact]
    public void Options_IsEmpty ()
    {
        DateClet clet = new ();

        Assert.Empty (clet.Options);
    }

    [Fact]
    public void AcceptsPositionalArgs_IsFalse ()
    {
        ICliCommand clet = new DateClet ();

        Assert.False (clet.AcceptsPositionalArgs);
    }
}
