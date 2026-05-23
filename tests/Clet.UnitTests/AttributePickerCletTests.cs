using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UnitTests;

public class AttributePickerCletTests
{
    [Fact]
    public void PrimaryAlias_IsAttributePicker ()
    {
        AttributePickerClet clet = new ();

        Assert.Equal ("attribute-picker", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        AttributePickerClet clet = new ();

        Assert.Equal (CommandKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsJsonObject ()
    {
        AttributePickerClet clet = new ();

        Assert.Equal (typeof (System.Text.Json.Nodes.JsonObject), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        AttributePickerClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsAttributePicker ()
    {
        AttributePickerClet clet = new ();

        Assert.Contains ("attribute-picker", clet.Aliases);
    }

    [Fact]
    public void Options_IsEmpty ()
    {
        AttributePickerClet clet = new ();

        Assert.Empty (clet.Options);
    }

    [Fact]
    public void AcceptsPositionalArgs_IsFalse ()
    {
        ICliCommand clet = new AttributePickerClet ();

        Assert.False (clet.AcceptsPositionalArgs);
    }
}
