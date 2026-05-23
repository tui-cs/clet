using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UnitTests;

public class MarkdownCletTests
{
    [Fact]
    public void PrimaryAlias_IsMd ()
    {
        MarkdownClet clet = new ();

        Assert.Equal ("md", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsViewer ()
    {
        MarkdownClet clet = new ();

        Assert.Equal (CommandKind.Viewer, clet.Kind);
    }

    [Fact]
    public void ResultType_IsVoid ()
    {
        MarkdownClet clet = new ();

        Assert.Equal (typeof (void), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        MarkdownClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsMd ()
    {
        MarkdownClet clet = new ();

        Assert.Contains ("md", clet.Aliases);
    }

    [Fact]
    public void Aliases_ContainsMarkdown ()
    {
        MarkdownClet clet = new ();

        Assert.Contains ("markdown", clet.Aliases);
    }

    [Fact]
    public void Options_ContainsThemeCatAndNoBrowse ()
    {
        MarkdownClet clet = new ();

        Assert.Equal (3, clet.Options.Count);
        Assert.Equal ("theme", clet.Options[0].Name);
        Assert.False (clet.Options[0].Required);
        Assert.Equal ("cat", clet.Options[1].Name);
        Assert.False (clet.Options[1].Required);
        Assert.Equal ("no-browse", clet.Options[2].Name);
        Assert.False (clet.Options[2].Required);
    }

    [Fact]
    public void AcceptsPositionalArgs_IsTrue ()
    {
        MarkdownClet clet = new ();

        Assert.True (clet.AcceptsPositionalArgs);
    }

    [Fact]
    public void TryResolveLocalMarkdownLink_WithFragment_ExtractsFragment ()
    {
        string tempDir = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());
        Directory.CreateDirectory (tempDir);
        string tempFile = Path.Combine (tempDir, "test.md");
        File.WriteAllText (tempFile, "# Heading");

        try
        {
            FileAccessPolicy policy = new (tempDir, null, false);

            bool result = MarkdownClet.TryResolveLocalMarkdownLink (
                "test.md#heading", tempDir, policy, out string? resolvedPath, out string? fragment);

            Assert.True (result);
            Assert.Equal (Path.GetFullPath (tempFile), resolvedPath);
            Assert.Equal ("heading", fragment);
        }
        finally
        {
            Directory.Delete (tempDir, true);
        }
    }

    [Fact]
    public void TryResolveLocalMarkdownLink_WithoutFragment_FragmentIsNull ()
    {
        string tempDir = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());
        Directory.CreateDirectory (tempDir);
        string tempFile = Path.Combine (tempDir, "test.md");
        File.WriteAllText (tempFile, "# Heading");

        try
        {
            FileAccessPolicy policy = new (tempDir, null, false);

            bool result = MarkdownClet.TryResolveLocalMarkdownLink (
                "test.md", tempDir, policy, out string? resolvedPath, out string? fragment);

            Assert.True (result);
            Assert.Equal (Path.GetFullPath (tempFile), resolvedPath);
            Assert.Null (fragment);
        }
        finally
        {
            Directory.Delete (tempDir, true);
        }
    }

    [Fact]
    public void TryResolveLocalMarkdownLink_PureFragment_ReturnsFalse ()
    {
        string tempDir = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());
        Directory.CreateDirectory (tempDir);

        try
        {
            FileAccessPolicy policy = new (tempDir, null, false);

            bool result = MarkdownClet.TryResolveLocalMarkdownLink (
                "#heading", tempDir, policy, out string? resolvedPath, out string? fragment);

            Assert.False (result);
            Assert.Null (resolvedPath);
            Assert.Equal ("heading", fragment);
        }
        finally
        {
            Directory.Delete (tempDir, true);
        }
    }
}
