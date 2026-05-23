using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UnitTests;

public class IntCletTests
{
    [Fact]
    public void PrimaryAlias_IsInt ()
    {
        IntClet clet = new ();

        Assert.Equal ("int", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        IntClet clet = new ();

        Assert.Equal (CommandKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsInt ()
    {
        IntClet clet = new ();

        Assert.Equal (typeof (int), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        IntClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsInt ()
    {
        IntClet clet = new ();

        Assert.Contains ("int", clet.Aliases);
    }

    [Fact]
    public void Options_ContainsStep ()
    {
        IntClet clet = new ();

        Assert.Single (clet.Options);
        Assert.Equal ("step", clet.Options[0].Name);
        Assert.False (clet.Options[0].Required);
    }

    [Fact]
    public void AcceptsPositionalArgs_IsFalse ()
    {
        ICliCommand clet = new IntClet ();

        Assert.False (clet.AcceptsPositionalArgs);
    }

    [Theory]
    [InlineData ("42", true)]
    [InlineData ("-7", true)]
    [InlineData ("abc", false)]
    [InlineData ("3.14", false)]
    public void TryValidateInitial_ValidatesIntString (string initial, bool expected)
    {
        IntClet clet = new ();
        CommandRunOptions options = new ();

        Assert.Equal (expected, clet.TryValidateInitial (initial, options));
    }
}
