using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UnitTests;

public class DecimalCletTests
{
    [Fact]
    public void PrimaryAlias_IsDecimal ()
    {
        DecimalClet clet = new ();

        Assert.Equal ("decimal", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        DecimalClet clet = new ();

        Assert.Equal (CommandKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsDecimal ()
    {
        DecimalClet clet = new ();

        Assert.Equal (typeof (decimal), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        DecimalClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsDecimal ()
    {
        DecimalClet clet = new ();

        Assert.Contains ("decimal", clet.Aliases);
    }

    [Fact]
    public void Options_ContainsStep ()
    {
        DecimalClet clet = new ();

        Assert.Single (clet.Options);
        Assert.Equal ("step", clet.Options[0].Name);
        Assert.False (clet.Options[0].Required);
    }

    [Fact]
    public void Options_StepDefault_IsFractional ()
    {
        DecimalClet clet = new ();

        Assert.Equal ("0.1", clet.Options[0].DefaultValue);
    }

    [Fact]
    public void AcceptsPositionalArgs_IsFalse ()
    {
        ICliCommand clet = new DecimalClet ();

        Assert.False (clet.AcceptsPositionalArgs);
    }
}
