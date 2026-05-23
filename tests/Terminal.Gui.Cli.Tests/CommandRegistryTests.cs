using Xunit;

namespace Terminal.Gui.Cli.Tests;

public class CommandRegistryTests
{
    [Fact]
    public void Register_AddsCommand_ResolvableByAlias ()
    {
        CommandRegistry registry = new ();
        StubCommand cmd = new ("test", ["test", "t"]);

        registry.Register (cmd);

        Assert.True (registry.TryResolve ("test", out ICliCommand? resolved));
        Assert.Same (cmd, resolved);
    }

    [Fact]
    public void TryResolve_CaseInsensitive ()
    {
        CommandRegistry registry = new ();
        StubCommand cmd = new ("select", ["select", "sel"]);
        registry.Register (cmd);

        Assert.True (registry.TryResolve ("SELECT", out ICliCommand? resolved));
        Assert.Same (cmd, resolved);
    }

    [Fact]
    public void TryResolve_ShortAlias ()
    {
        CommandRegistry registry = new ();
        StubCommand cmd = new ("select", ["select", "sel"]);
        registry.Register (cmd);

        Assert.True (registry.TryResolve ("sel", out ICliCommand? resolved));
        Assert.Same (cmd, resolved);
    }

    [Fact]
    public void TryResolve_UnknownAlias_ReturnsFalse ()
    {
        CommandRegistry registry = new ();

        Assert.False (registry.TryResolve ("nope", out _));
    }

    [Fact]
    public void Register_DuplicateAlias_Throws ()
    {
        CommandRegistry registry = new ();
        StubCommand cmd1 = new ("a", ["a", "shared"]);
        StubCommand cmd2 = new ("b", ["b", "shared"]);
        registry.Register (cmd1);

        Assert.Throws<InvalidOperationException> (() => registry.Register (cmd2));
    }

    [Fact]
    public void All_ReturnsRegisteredCommands ()
    {
        CommandRegistry registry = new ();
        StubCommand cmd1 = new ("a", ["a"]);
        StubCommand cmd2 = new ("b", ["b"]);
        registry.Register (cmd1);
        registry.Register (cmd2);

        Assert.Equal (2, registry.All.Count);
        Assert.Contains (cmd1, registry.All);
        Assert.Contains (cmd2, registry.All);
    }

    [Fact]
    public void Register_PrimaryAliasNotInAliases_Throws ()
    {
        CommandRegistry registry = new ();
        StubCommand cmd = new ("primary", ["other"]);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException> (() => registry.Register (cmd));
        Assert.Contains ("PrimaryAlias", ex.Message);
    }
}
