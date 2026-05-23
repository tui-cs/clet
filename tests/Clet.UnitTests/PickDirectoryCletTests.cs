using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UnitTests;

public class PickDirectoryCletTests
{
    [Fact]
    public void PrimaryAlias_IsPickDirectory ()
    {
        PickDirectoryClet clet = new ();

        Assert.Equal ("pick-directory", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        PickDirectoryClet clet = new ();

        Assert.Equal (CommandKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsString ()
    {
        PickDirectoryClet clet = new ();

        Assert.Equal (typeof (string), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        PickDirectoryClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsPickDirectory ()
    {
        PickDirectoryClet clet = new ();

        Assert.Contains ("pick-directory", clet.Aliases);
    }

    [Fact]
    public void Options_ContainsRoot ()
    {
        PickDirectoryClet clet = new ();

        Assert.Single (clet.Options);
        Assert.Equal ("root", clet.Options[0].Name);
        Assert.False (clet.Options[0].Required);
    }

    [Fact]
    public void AcceptsPositionalArgs_IsFalse ()
    {
        ICliCommand clet = new PickDirectoryClet ();

        Assert.False (clet.AcceptsPositionalArgs);
    }
}
