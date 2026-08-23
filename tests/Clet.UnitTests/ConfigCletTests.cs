using System.Text.Json;
using Xunit;

namespace Clet.UnitTests;

public class ConfigCletTests
{
    [Fact]
    public void DefaultConfigContent_IsValidJsonc ()
    {
        // Terminal.Gui's configuration loader uses these options
        JsonDocumentOptions options = new ()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        using var doc = JsonDocument.Parse (ConfigClet.DefaultConfigContent, options);
        Assert.NotNull (doc);
        Assert.Equal (JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Theory]
    [InlineData ("// \"Theme\": \"Anders\",", "\"Theme\": \"Anders\",")]
    [InlineData ("// \"Key\": { \"Separator\": \"+\" },", "\"Key\": { \"Separator\": \"+\" },")]
    [InlineData ("// \"Driver\": { \"Force16Colors\": false },", "\"Driver\": { \"Force16Colors\": false },")]
    [InlineData ("// \"Application\": { \"IsMouseDisabled\": false },", "\"Application\": { \"IsMouseDisabled\": false },")]
    [InlineData ("// \"PopoverMenu\": { \"DefaultKey\": \"Shift+F10\" },", "\"PopoverMenu\": { \"DefaultKey\": \"Shift+F10\" },")]
    public void DefaultConfigContent_UncommentSingleSetting_IsValidJsonc (string commented, string uncommented)
    {
        string modified = ConfigClet.DefaultConfigContent.Replace (commented, uncommented);

        // Verify the replacement actually happened
        Assert.DoesNotContain (commented, modified);
        Assert.Contains (uncommented, modified);

        JsonDocumentOptions options = new ()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        using var doc = JsonDocument.Parse (modified, options);
        Assert.NotNull (doc);
        Assert.Equal (JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void GetConfigPath_ReturnsPathUnderTuiDir ()
    {
        string path = ConfigClet.GetConfigPath ();

        Assert.EndsWith (ConfigClet.ConfigFileName, path);
        Assert.Contains (".tui", path);
    }

    [Fact]
    public void EnsureConfigFile_CreatesFileWithDefaultContent ()
    {
        string tempDir = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ():N}");
        string tempPath = Path.Combine (tempDir, ConfigClet.ConfigFileName);

        try
        {
            ConfigClet.EnsureConfigFile (tempPath);

            Assert.True (File.Exists (tempPath));
            string content = File.ReadAllText (tempPath);
            Assert.Equal (ConfigClet.DefaultConfigContent, content);
        }
        finally
        {
            if (Directory.Exists (tempDir))
            {
                Directory.Delete (tempDir, true);
            }
        }
    }

    [Fact]
    public void EnsureConfigFile_DoesNotOverwriteExistingFile ()
    {
        string tempDir = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ():N}");
        string tempPath = Path.Combine (tempDir, ConfigClet.ConfigFileName);
        Directory.CreateDirectory (tempDir);

        try
        {
            string existingContent = "{ \"existing\": true }";
            File.WriteAllText (tempPath, existingContent);

            ConfigClet.EnsureConfigFile (tempPath);

            string content = File.ReadAllText (tempPath);
            Assert.Equal (existingContent, content);
        }
        finally
        {
            Directory.Delete (tempDir, true);
        }
    }
}
