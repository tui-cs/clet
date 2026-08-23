using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Terminal.Gui.Configuration;
using Xunit;

namespace Clet.ConfigTests;

/// <summary>
/// Tests for <see cref="EditorSettings"/> round-tripping through the nested
/// <c>"EditorSettings"</c> section of the MEC-based configuration
/// (<see cref="CletConfiguration"/> / <see cref="TuiConfigurationBuilder"/>).
/// </summary>
public class EditorSettingsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly string? _originalHome;

    public EditorSettingsTests ()
    {
        _tempDir = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ():N}");
        string tuiDir = Path.Combine (_tempDir, ".tui");
        Directory.CreateDirectory (tuiDir);
        _configPath = Path.Combine (tuiDir, ConfigClet.ConfigFileName);

        // Save original HOME so we can restore it on cleanup.
        _originalHome = Environment.GetEnvironmentVariable ("HOME");

        // Point HOME at our temp directory (used by Save's config reload on Linux).
        Environment.SetEnvironmentVariable ("HOME", _tempDir);

        ResetEditorSettingsToDefaults ();
    }

    public void Dispose ()
    {
        ResetEditorSettingsToDefaults ();

        // Restore original HOME.
        Environment.SetEnvironmentVariable ("HOME", _originalHome);

        if (Directory.Exists (_tempDir))
        {
            Directory.Delete (_tempDir, true);
        }
    }

    /// <summary>Resets the static <see cref="EditorSettings"/> properties to their hard-coded defaults.</summary>
    private static void ResetEditorSettingsToDefaults ()
    {
        EditorSettings.LineNumbers = true;
        EditorSettings.FoldIndicators = true;
        EditorSettings.WordWrap = false;
        EditorSettings.ShowTabs = false;
        EditorSettings.Scrollbars = true;
        EditorSettings.IndentSize = 4;
        EditorSettings.ConvertTabsToSpaces = true;
        EditorSettings.AutoIndent = false;
        EditorSettings.AutoComplete = false;
    }

    /// <summary>
    /// Builds a bare MEC configuration from inline JSON via Terminal.Gui's
    /// runtime-config source (validates the nested 2.5+ shape) and returns
    /// the <c>"EditorSettings"</c> section.
    /// </summary>
    private static IConfiguration EditorSection (string json)
    {
        IConfigurationBuilder builder = new ConfigurationBuilder ().AddTuiRuntimeConfig (json);

        return builder.Build ().GetSection (EditorSettings.SectionName);
    }

    [Fact]
    public void ManagedKeys_ContainsAllProperties ()
    {
        Assert.Contains ("LineNumbers", EditorSettings.ManagedKeys);
        Assert.Contains ("FoldIndicators", EditorSettings.ManagedKeys);
        Assert.Contains ("WordWrap", EditorSettings.ManagedKeys);
        Assert.Contains ("ShowTabs", EditorSettings.ManagedKeys);
        Assert.Contains ("Scrollbars", EditorSettings.ManagedKeys);
        Assert.Contains ("IndentSize", EditorSettings.ManagedKeys);
        Assert.Contains ("ConvertTabsToSpaces", EditorSettings.ManagedKeys);
        Assert.Contains ("AutoIndent", EditorSettings.ManagedKeys);
        Assert.Contains ("AutoComplete", EditorSettings.ManagedKeys);
        Assert.Equal (9, EditorSettings.ManagedKeys.Count);
    }

    // Claude - Fable 5
    [Fact]
    public void Load_EmptySection_LeavesValuesUnchanged ()
    {
        EditorSettings.LineNumbers = false;
        EditorSettings.IndentSize = 7;

        EditorSettings.Load (EditorSection ("{}"));

        Assert.False (EditorSettings.LineNumbers);
        Assert.Equal (7, EditorSettings.IndentSize);
    }

    [Fact]
    public void Save_WritesAllKeys_ToConfigFile ()
    {
        // Arrange — set known values
        EditorSettings.LineNumbers = false;
        EditorSettings.FoldIndicators = false;
        EditorSettings.WordWrap = true;
        EditorSettings.ShowTabs = true;
        EditorSettings.Scrollbars = false;
        EditorSettings.IndentSize = 2;
        EditorSettings.ConvertTabsToSpaces = false;
        EditorSettings.AutoIndent = true;
        EditorSettings.AutoComplete = true;

        // Write a minimal config file so Save can insert into it
        File.WriteAllText (_configPath, "{}");

        // Act
        EditorSettings.Save (_configPath);

        // Assert — parse the written file and check the nested section values
        JsonObject section = ReadEditorSection (_configPath);

        Assert.False ((bool)section["LineNumbers"]!);
        Assert.False ((bool)section["FoldIndicators"]!);
        Assert.True ((bool)section["WordWrap"]!);
        Assert.True ((bool)section["ShowTabs"]!);
        Assert.False ((bool)section["Scrollbars"]!);
        Assert.Equal (2, (int)section["IndentSize"]!);
        Assert.False ((bool)section["ConvertTabsToSpaces"]!);
        Assert.True ((bool)section["AutoIndent"]!);
        Assert.True ((bool)section["AutoComplete"]!);
    }

    [Fact]
    public void Save_PreservesExistingKeys ()
    {
        // Arrange
        File.WriteAllText (
            _configPath,
            """
            {
              "$schema": "https://example.com/schema.json",
              "Theme": "Dark"
            }
            """);

        EditorSettings.LineNumbers = true;

        // Act
        EditorSettings.Save (_configPath);

        // Assert
        JsonObject obj = ReadRoot (_configPath);

        Assert.Equal ("https://example.com/schema.json", (string)obj["$schema"]!);
        Assert.Equal ("Dark", (string)obj["Theme"]!);
        Assert.True ((bool)obj[EditorSettings.SectionName]!["LineNumbers"]!);
    }

    [Fact]
    public void Save_PreservesJsoncComments ()
    {
        // Arrange — write a JSONC file with comments
        string jsonc =
            """
            {
              // This is a comment
              "$schema": "https://example.com/schema.json",

              // Theme configuration
              // "Theme": "Anders",

              "Key": { "Separator": "+" }
            }
            """;
        File.WriteAllText (_configPath, jsonc);

        EditorSettings.LineNumbers = false;
        EditorSettings.IndentSize = 2;

        // Act
        EditorSettings.Save (_configPath);

        // Assert — JSONC comments and existing keys are preserved
        string result = File.ReadAllText (_configPath);

        Assert.Contains ("// This is a comment", result);
        Assert.Contains ("// Theme configuration", result);
        Assert.Contains ("// \"Theme\": \"Anders\",", result);
        Assert.Contains ("\"Key\": { \"Separator\": \"+\" }", result);
        Assert.Contains ("\"$schema\": \"https://example.com/schema.json\"", result);
        Assert.Contains ("\"LineNumbers\": false", result);
        Assert.Contains ("\"IndentSize\": 2", result);
        Assert.False ((bool)ReadEditorSection (_configPath)["LineNumbers"]!);
    }

    [Fact]
    public void Save_PreservesDefaultConfigContent ()
    {
        // Arrange — use the full ConfigClet default JSONC template
        File.WriteAllText (_configPath, ConfigClet.DefaultConfigContent);

        EditorSettings.LineNumbers = false;

        // Act
        EditorSettings.Save (_configPath);

        // Assert — the original JSONC structure is intact
        string result = File.ReadAllText (_configPath);

        Assert.Contains ("clet configuration", result);
        Assert.Contains ("$schema", result);
        Assert.False ((bool)ReadEditorSection (_configPath)["LineNumbers"]!);
    }

    [Fact]
    public void Save_UpdatesExistingEditorSettingsKeys ()
    {
        // Arrange — write a file that already has an EditorSettings section
        File.WriteAllText (
            _configPath,
            """
            {
              // comments
              "EditorSettings": {
                "LineNumbers": true,
                "IndentSize": 4
              }
            }
            """);

        EditorSettings.LineNumbers = false;
        EditorSettings.IndentSize = 8;

        // Act
        EditorSettings.Save (_configPath);

        // Assert — existing keys are updated in place
        string result = File.ReadAllText (_configPath);

        Assert.Contains ("// comments", result);
        Assert.Contains ("\"LineNumbers\": false", result);
        Assert.Contains ("\"IndentSize\": 8", result);

        // Verify no duplicate keys or duplicate sections
        Assert.Equal (1, CountOccurrences (result, "\"LineNumbers\""));
        Assert.Equal (1, CountOccurrences (result, "\"IndentSize\""));
        Assert.Equal (1, CountOccurrences (result, "\"EditorSettings\""));
    }

    [Fact]
    public void Save_DoesNotModifyCommentedOutKeys ()
    {
        // Arrange — the section has a commented-out key
        File.WriteAllText (
            _configPath,
            """
            {
              "EditorSettings": {
                // "LineNumbers": true,
                "IndentSize": 4
              }
            }
            """);

        EditorSettings.LineNumbers = false;
        EditorSettings.IndentSize = 2;

        // Act
        EditorSettings.Save (_configPath);

        // Assert — commented-out key is untouched, active key is updated
        string result = File.ReadAllText (_configPath);

        Assert.Contains ("// \"LineNumbers\": true,", result);
        Assert.Contains ("\"IndentSize\": 2", result);
        Assert.Contains ("\"LineNumbers\": false", result);
    }

    // Claude - Fable 5
    [Fact]
    public void Save_SectionEndsWithTrailingLineComment_ProducesValidJsonc ()
    {
        // Arrange — the section's last entry carries a trailing line comment and
        // most managed keys are missing, so Save must insert them after it.
        File.WriteAllText (
            _configPath,
            """
            {
              "EditorSettings": {
                "LineNumbers": true // show line numbers
              }
            }
            """);

        EditorSettings.LineNumbers = false;

        // Act
        EditorSettings.Save (_configPath);

        // Assert — the file still parses as JSONC (the separating comma must land
        // after the value, not inside the comment) and carries all managed keys.
        JsonObject section = ReadEditorSection (_configPath);

        Assert.False ((bool)section["LineNumbers"]!);
        Assert.Equal (EditorSettings.ManagedKeys.Count, section.Count);
        Assert.Contains ("// show line numbers", File.ReadAllText (_configPath));
    }

    // Claude - Fable 5
    [Fact]
    public void Save_TopLevelEndsWithTrailingLineComment_ProducesValidJsonc ()
    {
        // Arrange — no EditorSettings section yet; the last top-level value has a
        // trailing line comment, so the inserted section needs a comma after it.
        File.WriteAllText (
            _configPath,
            """
            {
              "Theme": "Dark" // preferred theme
            }
            """);

        EditorSettings.IndentSize = 3;

        // Act
        EditorSettings.Save (_configPath);

        // Assert — the file still parses as JSONC and both the old key and the
        // new section are present.
        JsonObject root = ReadRoot (_configPath);

        Assert.Equal ("Dark", (string)root["Theme"]!);
        Assert.Equal (3, (int)ReadEditorSection (_configPath)["IndentSize"]!);
        Assert.Contains ("// preferred theme", File.ReadAllText (_configPath));
    }

    [Fact]
    public void RoundTrip_Load_RestoresPersistedValues ()
    {
        // Arrange — nested JSON with non-default values
        string json = """
            {
              "EditorSettings": {
                "LineNumbers": false,
                "IndentSize": 8,
                "WordWrap": true,
                "AutoIndent": true,
                "Scrollbars": false,
                "AutoComplete": true
              }
            }
            """;

        // Reset to defaults first
        ResetEditorSettingsToDefaults ();

        // Act — bind from inline JSON (cross-platform; avoids ~ resolution
        // issues on Windows where GetFolderPath ignores env var changes).
        EditorSettings.Load (EditorSection (json));

        // Assert
        Assert.False (EditorSettings.LineNumbers);
        Assert.Equal (8, EditorSettings.IndentSize);
        Assert.True (EditorSettings.WordWrap);
        Assert.True (EditorSettings.AutoIndent);
        Assert.False (EditorSettings.Scrollbars);
        Assert.True (EditorSettings.AutoComplete);
    }

    [Fact]
    public void RoundTrip_SaveThenLoad_RestoresValues ()
    {
        // Arrange — write initial config, set non-default values, save
        File.WriteAllText (_configPath, "{}");

        EditorSettings.LineNumbers = false;
        EditorSettings.FoldIndicators = false;
        EditorSettings.IndentSize = 3;
        EditorSettings.ConvertTabsToSpaces = false;
        EditorSettings.Save (_configPath);

        // Reset in-memory to defaults
        ResetEditorSettingsToDefaults ();

        // Act — load the saved file via inline JSON (cross-platform).
        string savedJson = File.ReadAllText (_configPath);
        EditorSettings.Load (EditorSection (savedJson));

        // Assert — values should match what we saved
        Assert.False (EditorSettings.LineNumbers);
        Assert.False (EditorSettings.FoldIndicators);
        Assert.Equal (3, EditorSettings.IndentSize);
        Assert.False (EditorSettings.ConvertTabsToSpaces);
    }

    [Fact]
    public void Save_CreatesConfigFile_WhenMissing ()
    {
        // Arrange — no config file exists yet
        Assert.False (File.Exists (_configPath));

        EditorSettings.IndentSize = 6;

        // Act
        EditorSettings.Save (_configPath);

        // Assert — file was created and contains the setting
        Assert.True (File.Exists (_configPath));

        Assert.Equal (6, (int)ReadEditorSection (_configPath)["IndentSize"]!);
    }

    [Fact]
    public void AllowedPaths_Load_BindsArrayFromNestedSection ()
    {
        // Arrange — reset in-memory value to empty
        List<string> savedPaths = [.. FileAccessSettings.AllowedPaths];
        FileAccessSettings.AllowedPaths = [];

        string json = """
            {
              "FileAccessSettings": {
                "AllowedPaths": ["/allowed/path1", "/allowed/path2"]
              }
            }
            """;

        try
        {
            // Act — bind from inline JSON
            IConfigurationBuilder builder = new ConfigurationBuilder ().AddTuiRuntimeConfig (json);
            FileAccessSettings.Load (builder.Build ().GetSection (FileAccessSettings.SectionName));

            // Assert — the array was applied
            Assert.NotEmpty (FileAccessSettings.AllowedPaths);
            Assert.Contains ("/allowed/path1", FileAccessSettings.AllowedPaths);
            Assert.Contains ("/allowed/path2", FileAccessSettings.AllowedPaths);
        }
        finally
        {
            FileAccessSettings.AllowedPaths = savedPaths;
        }
    }

    /// <summary>Parses the JSONC config file and returns the root object.</summary>
    private static JsonObject ReadRoot (string path)
    {
        var root = JsonNode.Parse (
            File.ReadAllText (path),
            documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

        Assert.NotNull (root);

        return Assert.IsType<JsonObject> (root);
    }

    /// <summary>Parses the JSONC config file and returns the nested <c>"EditorSettings"</c> object.</summary>
    private static JsonObject ReadEditorSection (string path)
    {
        JsonNode? section = ReadRoot (path)[EditorSettings.SectionName];
        Assert.NotNull (section);

        return Assert.IsType<JsonObject> (section);
    }

    /// <summary>Counts the number of occurrences of <paramref name="substring"/> in <paramref name="text"/>.</summary>
    private static int CountOccurrences (string text, string substring)
    {
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf (substring, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += substring.Length;
        }

        return count;
    }
}
