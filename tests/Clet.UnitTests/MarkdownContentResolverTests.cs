using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UnitTests;

public class MarkdownContentResolverTests
{
    [Fact]
    public void Resolve_InlineContent_ReturnsContent ()
    {
        CommandRunOptions options = new ();

        MarkdownContentResolver.ResolveResult result = MarkdownContentResolver.Resolve ("# Hello", options, stdinReader: null);

        Assert.True (result.IsSuccess);
        Assert.Equal ("# Hello", result.Content);
        Assert.Empty (result.Files);
    }

    [Fact]
    public void Resolve_InlineContent_TakesPriorityOverStdin ()
    {
        CommandRunOptions options = new ();
        using StringReader stdin = new ("stdin content");

        MarkdownContentResolver.ResolveResult result = MarkdownContentResolver.Resolve ("# Inline", options, stdin);

        Assert.True (result.IsSuccess);
        Assert.Equal ("# Inline", result.Content);
    }

    [Fact]
    public void Resolve_Stdin_ReturnsContent ()
    {
        CommandRunOptions options = new ();
        using StringReader stdin = new ("# From Stdin");

        MarkdownContentResolver.ResolveResult result = MarkdownContentResolver.Resolve (null, options, stdin);

        Assert.True (result.IsSuccess);
        Assert.Equal ("# From Stdin", result.Content);
        Assert.Empty (result.Files);
    }

    [Fact]
    public void Resolve_EmptyStdin_ReturnsError ()
    {
        CommandRunOptions options = new ();
        using StringReader stdin = new ("");

        MarkdownContentResolver.ResolveResult result = MarkdownContentResolver.Resolve (null, options, stdin);

        Assert.False (result.IsSuccess);
        Assert.Equal ("io", result.ErrorCode);
    }

    [Fact]
    public void Resolve_NoSource_ReturnsError ()
    {
        CommandRunOptions options = new ();

        MarkdownContentResolver.ResolveResult result = MarkdownContentResolver.Resolve (null, options, stdinReader: null);

        Assert.False (result.IsSuccess);
        Assert.Equal ("io", result.ErrorCode);
        Assert.Contains ("No file specified", result.ErrorMessage!);
    }

    [Fact]
    public void Resolve_FileArgs_ReadsExistingFiles ()
    {
        string tempDir = Path.Combine (Path.GetTempPath (), "clet-test-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (tempDir);

        try
        {
            string file = Path.Combine (tempDir, "test.md");
            File.WriteAllText (file, "# Test File");

            // Use allow-file extension to bypass CWD confinement — avoids process-global CWD race
            CommandRunOptions options = new ()
            {
                Arguments = [file],
                Extensions = new Dictionary<string, IReadOnlyList<string>> { ["allow-file"] = new List<string> { tempDir } },
            };

            MarkdownContentResolver.ResolveResult result = MarkdownContentResolver.Resolve (null, options, stdinReader: null);

            Assert.True (result.IsSuccess);
            Assert.Equal ("# Test File", result.Content);
            Assert.Single (result.Files);
        }
        finally
        {
            Directory.Delete (tempDir, true);
        }
    }

    [Fact]
    public void Resolve_FileArgs_NonexistentFile_ReturnsError ()
    {
        CommandRunOptions options = new () { Arguments = ["/nonexistent/file.md"] };

        MarkdownContentResolver.ResolveResult result = MarkdownContentResolver.Resolve (null, options, stdinReader: null);

        Assert.False (result.IsSuccess);
    }

    [Fact]
    public void Resolve_FileArgs_TakesPriorityOverInline ()
    {
        string tempDir = Path.Combine (Path.GetTempPath (), "clet-test-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (tempDir);

        try
        {
            string file = Path.Combine (tempDir, "priority.md");
            File.WriteAllText (file, "# From File");

            CommandRunOptions options = new ()
            {
                Arguments = [file],
                Extensions = new Dictionary<string, IReadOnlyList<string>> { ["allow-file"] = new List<string> { tempDir } },
            };

            MarkdownContentResolver.ResolveResult result = MarkdownContentResolver.Resolve ("# Inline", options, stdinReader: null);

            Assert.True (result.IsSuccess);
            Assert.Equal ("# From File", result.Content);
        }
        finally
        {
            Directory.Delete (tempDir, true);
        }
    }

    [Fact]
    public void ExpandFiles_NonexistentFile_ReturnsEmptyWithWarning ()
    {
        FileAccessPolicy policy = new (
            Directory.GetCurrentDirectory (),
            allowedFiles: null,
            allowBinary: false);

        List<string> files = MarkdownContentResolver.ExpandFiles (
            ["/nonexistent/file.md"],
            policy,
            out string? error);

        // File doesn't exist, so it's skipped (warning printed) and result is empty
        Assert.Empty (files);
        Assert.Null (error);
    }
}
