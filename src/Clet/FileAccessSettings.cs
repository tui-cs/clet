using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Terminal.Gui.App;

namespace Clet;

/// <summary>
/// Persistent file-access settings for <c>clet edit</c> and <c>clet md</c>.
/// Values are loaded from the nested <c>"FileAccessSettings"</c> section of
/// <c>~/.tui/clet.config.json</c> via <see cref="CletConfiguration"/>.
///
/// <para>Add directory paths to <see cref="AllowedPaths"/> to grant
/// permanent access without requiring <c>--allow-file</c> each time.</para>
/// </summary>
internal static class FileAccessSettings
{
    /// <summary>The JSON section name in the config file.</summary>
    internal const string SectionName = "FileAccessSettings";

    /// <summary>
    /// Directories (or files) that are permanently allowed for <c>clet edit</c>
    /// and <c>clet md</c>, regardless of the working directory.
    /// Equivalent to VS Code's trusted-folders list.
    /// </summary>
    /// <remarks>
    /// Set in <c>~/.tui/clet.config.json</c> as:
    /// <code>
    /// "FileAccessSettings": { "AllowedPaths": ["/home/user/projects", "/tmp/docs"] }
    /// </code>
    /// Files or directories listed here bypass extension and working-directory
    /// confinement checks (size and binary checks still apply).
    /// </remarks>
    public static List<string> AllowedPaths { get; set; } = [];

    /// <summary>
    /// Loads <see cref="AllowedPaths"/> from the <c>"FileAccessSettings"</c>
    /// configuration section. Leaves the current value unchanged when the
    /// section (or the <c>AllowedPaths</c> key) is absent.
    /// </summary>
    internal static void Load (IConfiguration section)
    {
        IConfigurationSection paths = section.GetSection ("AllowedPaths");

        if (!paths.Exists ())
        {
            return;
        }

        AllowedPaths = [.. paths.GetChildren ().Select (c => c.Value).OfType<string> ()];
    }

    /// <summary>
    /// Adds <paramref name="dirPath"/> to <see cref="AllowedPaths"/> both in memory
    /// and persistently in <c>~/.tui/clet.config.json</c>.
    /// The change takes effect immediately in the current session via the in-memory
    /// property; <see cref="CletConfiguration"/> picks up the persisted value on
    /// the next full reload cycle.
    /// </summary>
    /// <param name="dirPath">The directory (or file) path to trust.</param>
    internal static void AddToConfig (string dirPath) => AddToConfig (dirPath, ConfigClet.GetConfigPath ());

    /// <summary>
    /// Adds <paramref name="dirPath"/> to the persistent allow list stored in
    /// <paramref name="configPath"/>.  Separated from <see cref="AddToConfig(string)"/>
    /// for testability.
    /// </summary>
    [UnconditionalSuppressMessage ("Trimming", "IL2026",
        Justification = "Operates only on string values, which are primitive JSON types and trim-safe.")]
    [UnconditionalSuppressMessage ("AOT", "IL3050",
        Justification = "Operates only on string values, which are primitive JSON types and AOT-safe.")]
    internal static void AddToConfig (string dirPath, string configPath)
    {
        ConfigClet.EnsureConfigFile (configPath);

        try
        {
            string text = File.ReadAllText (configPath);

            // Parse the JSONC file — comments are stripped, but all active keys
            // (the EditorSettings section, etc.) are preserved in the rewritten file.
            var root = JsonNode.Parse (
                text,
                documentOptions: new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });

            if (root is not JsonObject obj)
            {
                return;
            }

            // Ensure the nested "FileAccessSettings" object exists.
            if (obj[SectionName] is not JsonObject section)
            {
                section = new JsonObject ();
                obj[SectionName] = section;
            }

            // Append to existing array or create a new one.
            // Implicit string → JsonNode cast avoids reflection (IL2026/IL3050).
            JsonNode dirPathNode = dirPath;

            if (section["AllowedPaths"] is JsonArray existing)
            {
                bool found = existing.Any (n => n?.GetValue<string> () == dirPath);

                if (!found)
                {
                    existing.Add (dirPathNode);
                }
            }
            else
            {
                section["AllowedPaths"] = new JsonArray (dirPathNode);
            }

            File.WriteAllText (configPath, obj.ToJsonString (new JsonSerializerOptions { WriteIndented = true }));

            // Update the in-memory property directly so it takes effect immediately
            // in the current session, without triggering a full configuration reload
            // that could race with other tests or components.
            // The persisted file is picked up on the next full reload cycle.
            if (!AllowedPaths.Contains (dirPath))
            {
                AllowedPaths = [.. AllowedPaths, dirPath];
            }
        }
        catch (Exception ex)
        {
            Logging.Error ($"FileAccessSettings.AddToConfig: {ex.GetType ().Name}: {ex.Message}");
        }
    }
}
