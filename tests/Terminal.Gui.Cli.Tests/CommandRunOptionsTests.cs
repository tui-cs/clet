using Xunit;

namespace Terminal.Gui.Cli.Tests;

public class CommandRunOptionsTests
{
    [Fact]
    public void GetExtension_ReturnsLastValue ()
    {
        CommandRunOptions options = new ()
        {
            Extensions = new Dictionary<string, IReadOnlyList<string>>
            {
                ["port"] = ["8080", "9090"]
            }
        };

        int? port = options.GetExtension ("port", int.Parse, 3000);

        Assert.Equal (9090, port);
    }

    [Fact]
    public void GetExtension_MissingKey_ReturnsDefault ()
    {
        CommandRunOptions options = new ();

        int? port = options.GetExtension ("port", int.Parse, 3000);

        Assert.Equal (3000, port);
    }

    [Fact]
    public void GetExtensionList_ReturnsAllValues ()
    {
        CommandRunOptions options = new ()
        {
            Extensions = new Dictionary<string, IReadOnlyList<string>>
            {
                ["allow-file"] = ["/tmp", "/home"]
            }
        };

        IReadOnlyList<string> files = options.GetExtensionList ("allow-file");

        Assert.Equal (2, files.Count);
        Assert.Equal ("/tmp", files[0]);
        Assert.Equal ("/home", files[1]);
    }

    [Fact]
    public void GetExtensionList_MissingKey_ReturnsEmpty ()
    {
        CommandRunOptions options = new ();

        IReadOnlyList<string> files = options.GetExtensionList ("nope");

        Assert.Empty (files);
    }

    [Fact]
    public void HasExtension_ReturnsTrue_WhenPresent ()
    {
        CommandRunOptions options = new ()
        {
            Extensions = new Dictionary<string, IReadOnlyList<string>>
            {
                ["no-browse"] = [""]
            }
        };

        Assert.True (options.HasExtension ("no-browse"));
    }

    [Fact]
    public void HasExtension_ReturnsFalse_WhenAbsent ()
    {
        CommandRunOptions options = new ();

        Assert.False (options.HasExtension ("no-browse"));
    }

    [Fact]
    public void Defaults_AreCorrect ()
    {
        CommandRunOptions options = new ();

        Assert.Null (options.Initial);
        Assert.Null (options.Title);
        Assert.False (options.JsonOutput);
        Assert.Null (options.Timeout);
        Assert.False (options.Fullscreen);
        Assert.False (options.Cat);
        Assert.Null (options.OutputPath);
        Assert.Null (options.Rows);
        Assert.Empty (options.Arguments);
        Assert.Empty (options.CommandOptions);
        Assert.Empty (options.Extensions);
    }
}
