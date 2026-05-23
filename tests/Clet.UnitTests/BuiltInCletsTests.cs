using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UnitTests;

public class BuiltInCletsTests
{
    [Fact]
    public void RegisterAll_RegistersSelect ()
    {
        ICommandRegistry registry = new CommandRegistry ();

        BuiltInClets.RegisterAll (registry);

        Assert.True (registry.TryResolve ("select", out ICliCommand? clet));
        Assert.NotNull (clet);
        Assert.Equal ("select", clet.PrimaryAlias);
        Assert.Equal (CommandKind.Input, clet.Kind);
    }

    [Theory]
    [InlineData ("select")]
    [InlineData ("text")]
    [InlineData ("multiline-text")]
    [InlineData ("mt")]
    [InlineData ("int")]
    [InlineData ("decimal")]
    [InlineData ("confirm")]
    [InlineData ("date")]
    [InlineData ("time")]
    [InlineData ("duration")]
    [InlineData ("color")]
    [InlineData ("multi-select")]
    [InlineData ("attribute-picker")]
    [InlineData ("attribute")]
    [InlineData ("pick-file")]
    [InlineData ("file")]
    [InlineData ("pick-directory")]
    [InlineData ("dir")]
    [InlineData ("linear-range")]
    [InlineData ("range")]
    public void RegisterAll_RegistersInputClet (string alias)
    {
        ICommandRegistry registry = new CommandRegistry ();
        BuiltInClets.RegisterAll (registry);

        Assert.True (registry.TryResolve (alias, out ICliCommand? clet));
        Assert.NotNull (clet);
        Assert.Equal (CommandKind.Input, clet.Kind);
    }

    [Theory]
    [InlineData ("edit")]
    [InlineData ("editor")]
    [InlineData ("md")]
    [InlineData ("markdown")]
    [InlineData ("help")]
    [InlineData ("config")]
    public void RegisterAll_RegistersViewerClet (string alias)
    {
        ICommandRegistry registry = new CommandRegistry ();
        BuiltInClets.RegisterAll (registry);

        Assert.True (registry.TryResolve (alias, out ICliCommand? clet));
        Assert.NotNull (clet);
        Assert.Equal (CommandKind.Viewer, clet.Kind);
    }

    [Fact]
    public void RegisterAll_Registers19Clets ()
    {
        CommandRegistry registry = new ();
        BuiltInClets.RegisterAll (registry);

        Assert.Equal (18, registry.All.Count);
    }
}
