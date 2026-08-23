using Microsoft.Extensions.Configuration;
using Terminal.Gui.Configuration;
using Xunit;

namespace Clet.ConfigTests;

/// <summary>
/// Tests for <see cref="FileAccessSettings"/> and
/// <see cref="FileAccessPolicy.MergeWithConfigPaths"/>.
/// All classes in this file touch <see cref="FileAccessSettings.AllowedPaths"/>,
/// which is a CM-discovered static property. They live in this non-parallel
/// assembly to avoid races with other test projects.
/// </summary>
public class FileAccessSettingsTests : IDisposable
{
    private readonly List<string> _originalPaths = [.. FileAccessSettings.AllowedPaths];

    public void Dispose ()
    {
        FileAccessSettings.AllowedPaths = _originalPaths;
    }

    // ── MergeWithConfigPaths ─────────────────────────────────────────────────

    [Fact]
    public void MergeWithConfigPaths_ConfigEmptyAndCliNull_ReturnsNull ()
    {
        FileAccessSettings.AllowedPaths = [];

        IReadOnlyList<string>? result = FileAccessPolicy.MergeWithConfigPaths (null);

        Assert.Null (result);
    }

    [Fact]
    public void MergeWithConfigPaths_EmptyConfig_ReturnsCli ()
    {
        FileAccessSettings.AllowedPaths = [];
        List<string> cli = ["/tmp/a"];

        IReadOnlyList<string>? result = FileAccessPolicy.MergeWithConfigPaths (cli);

        Assert.Same (cli, result);
    }

    [Fact]
    public void MergeWithConfigPaths_NullCli_ReturnsConfigPaths ()
    {
        FileAccessSettings.AllowedPaths = ["/tmp/config-dir"];

        IReadOnlyList<string>? result = FileAccessPolicy.MergeWithConfigPaths (null);

        Assert.NotNull (result);
        Assert.Single (result);
        Assert.Equal ("/tmp/config-dir", result[0]);
    }

    [Fact]
    public void MergeWithConfigPaths_BothProvided_CombinesCliThenConfig ()
    {
        FileAccessSettings.AllowedPaths = ["/config/path"];
        List<string> cli = ["/cli/path"];

        IReadOnlyList<string>? result = FileAccessPolicy.MergeWithConfigPaths (cli);

        Assert.NotNull (result);
        Assert.Equal (2, result.Count);
        Assert.Equal ("/cli/path", result[0]);
        Assert.Equal ("/config/path", result[1]);
    }

    // ── FileAccessPolicy integration with config paths ───────────────────────

    [Fact]
    public void Policy_ConfigPath_AllowsFileOutsideCwdWithoutCliFlag ()
    {
        string allowedDir = Path.Combine (Path.GetTempPath (), $"clet-cfg-allow-{Guid.NewGuid ():N}");
        string cwd = Path.Combine (Path.GetTempPath (), $"clet-cfg-cwd-{Guid.NewGuid ():N}");
        Directory.CreateDirectory (allowedDir);
        Directory.CreateDirectory (cwd);

        string file = Path.Combine (allowedDir, "notes.txt");
        File.WriteAllText (file, "hello");

        try
        {
            FileAccessSettings.AllowedPaths = [allowedDir];

            FileAccessPolicy policy = new (
                cwd,
                FileAccessPolicy.MergeWithConfigPaths (null),
                allowBinary: false);

            string? error = policy.CheckFile (file);

            Assert.Null (error);
        }
        finally
        {
            File.Delete (file);
            Directory.Delete (allowedDir, recursive: true);
            Directory.Delete (cwd, recursive: true);
        }
    }

    [Fact]
    public void Policy_ConfigPath_AllowsDisallowedExtension ()
    {
        string allowedDir = Path.Combine (Path.GetTempPath (), $"clet-cfg-ext-{Guid.NewGuid ():N}");
        string cwd = Path.Combine (Path.GetTempPath (), $"clet-cfg-cwd-ext-{Guid.NewGuid ():N}");
        Directory.CreateDirectory (allowedDir);
        Directory.CreateDirectory (cwd);

        string file = Path.Combine (allowedDir, "config.yaml");
        File.WriteAllText (file, "key: value");

        try
        {
            FileAccessSettings.AllowedPaths = [allowedDir];

            FileAccessPolicy policy = new (
                cwd,
                FileAccessPolicy.MergeWithConfigPaths (null),
                allowBinary: false);

            string? error = policy.CheckFile (file);

            Assert.Null (error);
        }
        finally
        {
            File.Delete (file);
            Directory.Delete (allowedDir, recursive: true);
            Directory.Delete (cwd, recursive: true);
        }
    }

    [Fact]
    public void Policy_ConfigPath_FileNotInAllowedDir_IsStillRefused ()
    {
        string allowedDir = Path.Combine (Path.GetTempPath (), $"clet-cfg-allow2-{Guid.NewGuid ():N}");
        string cwd = Path.Combine (Path.GetTempPath (), $"clet-cfg-cwd2-{Guid.NewGuid ():N}");
        string otherDir = Path.Combine (Path.GetTempPath (), $"clet-cfg-other-{Guid.NewGuid ():N}");
        Directory.CreateDirectory (allowedDir);
        Directory.CreateDirectory (cwd);
        Directory.CreateDirectory (otherDir);

        string file = Path.Combine (otherDir, "notes.conf");
        File.WriteAllText (file, "key=val");

        try
        {
            FileAccessSettings.AllowedPaths = [allowedDir];

            FileAccessPolicy policy = new (
                cwd,
                FileAccessPolicy.MergeWithConfigPaths (null),
                allowBinary: false);

            string? error = policy.CheckFile (file);

            Assert.NotNull (error);
            Assert.Contains ("Refused", error);
        }
        finally
        {
            File.Delete (file);
            Directory.Delete (allowedDir, recursive: true);
            Directory.Delete (cwd, recursive: true);
            Directory.Delete (otherDir, recursive: true);
        }
    }
}

/// <summary>
/// Verifies that <see cref="FileAccessSettings.Load"/> binds
/// <see cref="FileAccessSettings.AllowedPaths"/> from the nested
/// <c>"FileAccessSettings"</c> configuration section.
/// </summary>
public class FileAccessSettingsLoadTests
{
    // Claude - Fable 5
    [Fact]
    public void Load_BindsAllowedPaths_FromNestedSection ()
    {
        List<string> savedPaths = [.. FileAccessSettings.AllowedPaths];

        string json = """
            {
              "FileAccessSettings": {
                "AllowedPaths": ["/from/config"]
              }
            }
            """;

        try
        {
            IConfigurationBuilder builder = new ConfigurationBuilder ().AddTuiRuntimeConfig (json);
            FileAccessSettings.Load (builder.Build ().GetSection (FileAccessSettings.SectionName));

            Assert.Contains ("/from/config", FileAccessSettings.AllowedPaths);
        }
        finally
        {
            FileAccessSettings.AllowedPaths = savedPaths;
        }
    }

    // Claude - Fable 5
    [Fact]
    public void Load_MissingSection_LeavesValueUnchanged ()
    {
        List<string> savedPaths = [.. FileAccessSettings.AllowedPaths];
        FileAccessSettings.AllowedPaths = ["/existing"];

        try
        {
            IConfigurationBuilder builder = new ConfigurationBuilder ().AddTuiRuntimeConfig ("{}");
            FileAccessSettings.Load (builder.Build ().GetSection (FileAccessSettings.SectionName));

            Assert.Contains ("/existing", FileAccessSettings.AllowedPaths);
        }
        finally
        {
            FileAccessSettings.AllowedPaths = savedPaths;
        }
    }
}

/// <summary>
/// Tests for <see cref="FileAccessSettings.AddToConfig(string, string)"/>.
/// Mutates <see cref="FileAccessSettings.AllowedPaths"/>, a CM-discovered static property.
/// </summary>
public class FileAccessSettingsAddToConfigTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly List<string> _originalPaths;

    public FileAccessSettingsAddToConfigTests ()
    {
        _tempDir = Path.Combine (Path.GetTempPath (), $"clet-addcfg-{Guid.NewGuid ():N}");
        Directory.CreateDirectory (_tempDir);
        _configPath = Path.Combine (_tempDir, "clet.config.json");
        _originalPaths = [.. FileAccessSettings.AllowedPaths];
    }

    public void Dispose ()
    {
        FileAccessSettings.AllowedPaths = _originalPaths;

        if (Directory.Exists (_tempDir))
        {
            Directory.Delete (_tempDir, true);
        }
    }

    [Fact]
    public void AddToConfig_CreatesFileAndAddsPath ()
    {
        FileAccessSettings.AddToConfig ("/new/path", _configPath);

        Assert.True (File.Exists (_configPath));
        string text = File.ReadAllText (_configPath);
        Assert.Contains ("/new/path", text);
    }

    [Fact]
    public void AddToConfig_UpdatesInMemoryAllowedPaths ()
    {
        FileAccessSettings.AllowedPaths = [];

        FileAccessSettings.AddToConfig ("/in-memory", _configPath);

        Assert.Contains ("/in-memory", FileAccessSettings.AllowedPaths);
    }

    [Fact]
    public void AddToConfig_AppendsToPreviouslyWrittenPath ()
    {
        FileAccessSettings.AddToConfig ("/first", _configPath);
        FileAccessSettings.AddToConfig ("/second", _configPath);

        string text = File.ReadAllText (_configPath);
        Assert.Contains ("/first", text);
        Assert.Contains ("/second", text);
    }

    [Fact]
    public void AddToConfig_DuplicatePath_NotAddedAgain ()
    {
        FileAccessSettings.AddToConfig ("/dup", _configPath);
        FileAccessSettings.AddToConfig ("/dup", _configPath);

        string text = File.ReadAllText (_configPath);
        int occurrences = text.Split (["/dup"], StringSplitOptions.None).Length - 1;
        Assert.Equal (1, occurrences);
    }

    [Fact]
    public void AddToConfig_PreservesExistingKeys ()
    {
        // Write a config file that already has an EditorSettings section.
        File.WriteAllText (_configPath, """
            {
              "EditorSettings": {
                "LineNumbers": false
              }
            }
            """);

        FileAccessSettings.AddToConfig ("/extra", _configPath);

        string text = File.ReadAllText (_configPath);
        Assert.Contains ("LineNumbers", text);
        Assert.Contains ("/extra", text);
    }
}
