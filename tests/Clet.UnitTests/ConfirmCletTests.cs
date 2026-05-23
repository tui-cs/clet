using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UnitTests;

public class ConfirmCletTests
{
    [Fact]
    public void PrimaryAlias_IsConfirm ()
    {
        ConfirmClet clet = new ();

        Assert.Equal ("confirm", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        ConfirmClet clet = new ();

        Assert.Equal (CommandKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsBool ()
    {
        ConfirmClet clet = new ();

        Assert.Equal (typeof (bool), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        ConfirmClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsConfirm ()
    {
        ConfirmClet clet = new ();

        Assert.Contains ("confirm", clet.Aliases);
    }

    [Fact]
    public void Options_IsEmpty ()
    {
        ConfirmClet clet = new ();

        Assert.Empty (clet.Options);
    }

    [Fact]
    public void AcceptsPositionalArgs_IsFalse ()
    {
        ICliCommand clet = new ConfirmClet ();

        Assert.False (clet.AcceptsPositionalArgs);
    }
}
