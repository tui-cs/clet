using Xunit;

namespace Terminal.Gui.Cli.Tests;

public class ArgParserTests
{
    private static ArgParser CreateParser (params GlobalOptionDescriptor[] globals)
    {
        return new ArgParser ([.. globals]);
    }

    [Fact]
    public void EmptyArgs_ReturnsHelpRootFlag ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse ([]);

        Assert.True (result.Success);
        Assert.Equal (ArgParser.RootFlag.Help, result.RootFlag);
    }

    [Fact]
    public void Help_Flag_Detected ()
    {
        ArgParser parser = CreateParser ();

        Assert.Equal (ArgParser.RootFlag.Help, parser.Parse (["--help"]).RootFlag);
        Assert.Equal (ArgParser.RootFlag.Help, parser.Parse (["-h"]).RootFlag);
    }

    [Fact]
    public void Version_Flag_Detected ()
    {
        ArgParser parser = CreateParser ();

        Assert.Equal (ArgParser.RootFlag.Version, parser.Parse (["--version"]).RootFlag);
    }

    [Fact]
    public void OpenCli_Flag_Detected ()
    {
        ArgParser parser = CreateParser ();

        Assert.Equal (ArgParser.RootFlag.OpenCli, parser.Parse (["--opencli"]).RootFlag);
    }

    [Fact]
    public void Alias_IsParsed ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select"]);

        Assert.True (result.Success);
        Assert.Equal ("select", result.Alias);
    }

    [Fact]
    public void JsonFlag_SetsJsonOutput ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "--json"]);

        Assert.True (result.Options!.JsonOutput);
    }

    [Fact]
    public void JsonFlag_Short ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "-j"]);

        Assert.True (result.Options!.JsonOutput);
    }

    [Fact]
    public void FullscreenFlag ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "--fullscreen"]);

        Assert.True (result.Options!.Fullscreen);
    }

    [Fact]
    public void CatFlag ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["md", "--cat"]);

        Assert.True (result.Options!.Cat);
    }

    [Fact]
    public void Initial_WithValue ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "--initial", "hello"]);

        Assert.True (result.Success);
        Assert.Equal ("hello", result.Options!.Initial);
    }

    [Fact]
    public void Initial_Short ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "-i", "hello"]);

        Assert.Equal ("hello", result.Options!.Initial);
    }

    [Fact]
    public void Initial_MissingValue_Fails ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "--initial"]);

        Assert.False (result.Success);
        Assert.Contains ("--initial requires a value", result.Error);
    }

    [Fact]
    public void Title_WithValue ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "--title", "Pick one"]);

        Assert.Equal ("Pick one", result.Options!.Title);
    }

    [Fact]
    public void Timeout_Seconds ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "--timeout", "30s"]);

        Assert.Equal (TimeSpan.FromSeconds (30), result.Options!.Timeout);
    }

    [Fact]
    public void Timeout_Milliseconds ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "--timeout", "500ms"]);

        Assert.Equal (TimeSpan.FromMilliseconds (500), result.Options!.Timeout);
    }

    [Fact]
    public void Timeout_Minutes ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "--timeout", "2m"]);

        Assert.Equal (TimeSpan.FromMinutes (2), result.Options!.Timeout);
    }

    [Fact]
    public void Timeout_Invalid_Fails ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "--timeout", "abc"]);

        Assert.False (result.Success);
        Assert.Contains ("invalid --timeout", result.Error);
    }

    [Fact]
    public void Output_WithValue ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "--output", "/tmp/out.txt"]);

        Assert.Equal ("/tmp/out.txt", result.Options!.OutputPath);
    }

    [Fact]
    public void Rows_WithValue ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "--rows", "10"]);

        Assert.Equal (10, result.Options!.Rows);
    }

    [Fact]
    public void Rows_Invalid_Fails ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "--rows", "0"]);

        Assert.False (result.Success);
        Assert.Contains ("invalid --rows", result.Error);
    }

    [Fact]
    public void PositionalArgs_Collected ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "foo", "bar"]);

        Assert.Equal (["foo", "bar"], result.Options!.Arguments);
    }

    [Fact]
    public void DoubleDash_EndsOptions ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "--", "--json", "foo"]);

        Assert.False (result.Options!.JsonOutput);
        Assert.Equal (["--json", "foo"], result.Options!.Arguments);
    }

    [Fact]
    public void EqualsSign_Syntax ()
    {
        ArgParser parser = CreateParser ();
        ArgParser.ParseResult result = parser.Parse (["select", "--initial=hello"]);

        Assert.Equal ("hello", result.Options!.Initial);
    }

    [Fact]
    public void ConsumerGlobalOption_Flag ()
    {
        GlobalOptionDescriptor noBrowse = new ("no-browse", null, "Disable browsing", IsFlag: true);
        ArgParser parser = CreateParser (noBrowse);
        ArgParser.ParseResult result = parser.Parse (["md", "--no-browse"]);

        Assert.True (result.Options!.HasExtension ("no-browse"));
    }

    [Fact]
    public void ConsumerGlobalOption_Value ()
    {
        GlobalOptionDescriptor allowFile = new ("allow-file", null, "Permit file", IsFlag: false, Repeatable: true);
        ArgParser parser = CreateParser (allowFile);
        ArgParser.ParseResult result = parser.Parse (["edit", "--allow-file", "/tmp"]);

        IReadOnlyList<string> files = result.Options!.GetExtensionList ("allow-file");
        Assert.Single (files);
        Assert.Equal ("/tmp", files[0]);
    }

    [Fact]
    public void ConsumerGlobalOption_Repeatable_Accumulates ()
    {
        GlobalOptionDescriptor allowFile = new ("allow-file", null, "Permit file", IsFlag: false, Repeatable: true);
        ArgParser parser = CreateParser (allowFile);
        ArgParser.ParseResult result = parser.Parse (["edit", "--allow-file", "/tmp", "--allow-file", "/home"]);

        IReadOnlyList<string> files = result.Options!.GetExtensionList ("allow-file");
        Assert.Equal (2, files.Count);
        Assert.Equal ("/tmp", files[0]);
        Assert.Equal ("/home", files[1]);
    }

    [Fact]
    public void ConsumerGlobalOption_NonRepeatable_LastWins ()
    {
        GlobalOptionDescriptor port = new ("port", "p", "Port number", IsFlag: false, Repeatable: false);
        ArgParser parser = CreateParser (port);
        ArgParser.ParseResult result = parser.Parse (["serve", "--port", "8080", "--port", "9090"]);

        IReadOnlyList<string> values = result.Options!.GetExtensionList ("port");
        Assert.Single (values);
        Assert.Equal ("9090", values[0]);
    }

    [Fact]
    public void ConsumerGlobalOption_EqualsValueSyntax ()
    {
        GlobalOptionDescriptor allowFile = new ("allow-file", null, "Permit file", IsFlag: false, Repeatable: true);
        ArgParser parser = CreateParser (allowFile);
        ArgParser.ParseResult result = parser.Parse (["edit", "--allow-file=/tmp/foo"]);

        IReadOnlyList<string> files = result.Options!.GetExtensionList ("allow-file");
        Assert.Single (files);
        Assert.Equal ("/tmp/foo", files[0]);
    }

    [Fact]
    public void Initial_ExceedsMaxChars_Fails ()
    {
        ArgParser parser = new ([], maxInitialChars: 10);
        ArgParser.ParseResult result = parser.Parse (["select", "--initial", "12345678901"]);

        Assert.False (result.Success);
        Assert.Contains ("character limit", result.Error);
    }

    [Fact]
    public void TryParseTimeout_Seconds ()
    {
        Assert.True (ArgParser.TryParseTimeout ("30s", out TimeSpan t));
        Assert.Equal (TimeSpan.FromSeconds (30), t);
    }

    [Fact]
    public void TryParseTimeout_Milliseconds ()
    {
        Assert.True (ArgParser.TryParseTimeout ("500ms", out TimeSpan t));
        Assert.Equal (TimeSpan.FromMilliseconds (500), t);
    }

    [Fact]
    public void TryParseTimeout_Minutes ()
    {
        Assert.True (ArgParser.TryParseTimeout ("2m", out TimeSpan t));
        Assert.Equal (TimeSpan.FromMinutes (2), t);
    }

    [Fact]
    public void TryParseTimeout_Hours ()
    {
        Assert.True (ArgParser.TryParseTimeout ("1h", out TimeSpan t));
        Assert.Equal (TimeSpan.FromHours (1), t);
    }

    [Fact]
    public void TryParseTimeout_Invalid_ReturnsFalse ()
    {
        Assert.False (ArgParser.TryParseTimeout ("abc", out _));
        Assert.False (ArgParser.TryParseTimeout ("", out _));
        Assert.False (ArgParser.TryParseTimeout ("0s", out _));
        Assert.False (ArgParser.TryParseTimeout ("-5s", out _));
    }
}
