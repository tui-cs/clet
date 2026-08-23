using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Terminal.Gui.App;

namespace Clet;

/// <summary>
/// Persisted settings for the editor clet. Properties are loaded from the nested
/// <c>"EditorSettings"</c> section of <c>~/.tui/clet.config.json</c> via
/// <see cref="CletConfiguration"/> (Terminal.Gui's MEC-based configuration).
/// </summary>
internal static class EditorSettings
{
    /// <summary>The JSON section name in the config file.</summary>
    internal const string SectionName = "EditorSettings";

    // --- View toggles ---

    public static bool LineNumbers { get; set; } = true;

    public static bool FoldIndicators { get; set; } = true;

    public static bool WordWrap { get; set; }

    public static bool ShowTabs { get; set; }

    public static bool Scrollbars { get; set; } = true;

    // --- Tab settings ---

    public static int IndentSize { get; set; } = 4;

    public static bool ConvertTabsToSpaces { get; set; } = true;

    public static bool AutoIndent { get; set; }

    public static bool AutoComplete { get; set; }

    /// <summary>
    /// All keys managed by this class (inside the <see cref="SectionName"/> section).
    /// Used for selective persistence.
    /// </summary>
    private static readonly string[] Keys =
    [
        "LineNumbers",
        "FoldIndicators",
        "WordWrap",
        "ShowTabs",
        "Scrollbars",
        "IndentSize",
        "ConvertTabsToSpaces",
        "AutoIndent",
        "AutoComplete",
    ];

    /// <summary>
    /// Loads property values from the <c>"EditorSettings"</c> configuration section.
    /// Keys that are absent (or unparsable) leave the current value unchanged.
    /// </summary>
    internal static void Load (IConfiguration section)
    {
        LineNumbers = GetBool (section, "LineNumbers", LineNumbers);
        FoldIndicators = GetBool (section, "FoldIndicators", FoldIndicators);
        WordWrap = GetBool (section, "WordWrap", WordWrap);
        ShowTabs = GetBool (section, "ShowTabs", ShowTabs);
        Scrollbars = GetBool (section, "Scrollbars", Scrollbars);
        IndentSize = GetInt (section, "IndentSize", IndentSize);
        ConvertTabsToSpaces = GetBool (section, "ConvertTabsToSpaces", ConvertTabsToSpaces);
        AutoIndent = GetBool (section, "AutoIndent", AutoIndent);
        AutoComplete = GetBool (section, "AutoComplete", AutoComplete);
    }

    /// <summary>
    /// Saves current property values to <c>~/.tui/clet.config.json</c>,
    /// preserving all JSONC content (comments, formatting, non-editor keys).
    /// After writing, reloads <see cref="CletConfiguration"/> so that in-memory
    /// state matches the persisted file.
    /// </summary>
    internal static void Save () => Save (ConfigClet.GetConfigPath ());

    /// <summary>
    /// Saves current property values to the specified config file path,
    /// preserving all JSONC content (comments, formatting, non-editor keys).
    /// Values are written into the nested <c>"EditorSettings"</c> object.
    /// </summary>
    internal static void Save (string path)
    {
        ConfigClet.EnsureConfigFile (path);

        try
        {
            string text = File.ReadAllText (path);

            // Build key → JSON-value pairs for each managed setting.
            Dictionary<string, string> entries = new ()
            {
                ["LineNumbers"] = ToJson (LineNumbers),
                ["FoldIndicators"] = ToJson (FoldIndicators),
                ["WordWrap"] = ToJson (WordWrap),
                ["ShowTabs"] = ToJson (ShowTabs),
                ["Scrollbars"] = ToJson (Scrollbars),
                ["IndentSize"] = IndentSize.ToString (),
                ["ConvertTabsToSpaces"] = ToJson (ConvertTabsToSpaces),
                ["AutoIndent"] = ToJson (AutoIndent),
                ["AutoComplete"] = ToJson (AutoComplete),
            };

            text = UpsertSection (text, entries);

            File.WriteAllText (path, text);

            // Re-sync in-memory state from the configuration sources so it
            // matches the persisted file.
            CletConfiguration.Reload ();
        }
        catch (Exception ex)
        {
            Logging.Error ($"EditorSettings.Save: {ex.GetType ().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the keys managed by this class. Useful for testing.
    /// </summary>
    internal static IReadOnlyList<string> ManagedKeys => Keys;

    /// <summary>Converts a boolean to its JSON literal.</summary>
    private static string ToJson (bool value) => value ? "true" : "false";

    /// <summary>Reads a boolean value from a configuration section, falling back when absent or invalid.</summary>
    private static bool GetBool (IConfiguration section, string key, bool fallback) =>
        bool.TryParse (section[key], out bool value) ? value : fallback;

    /// <summary>Reads an integer value from a configuration section, falling back when absent or invalid.</summary>
    private static int GetInt (IConfiguration section, string key, int fallback) =>
        int.TryParse (section[key], out int value) ? value : fallback;

    /// <summary>
    /// Updates (or inserts) the nested <c>"EditorSettings"</c> object in the JSONC
    /// <paramref name="text"/>, replacing managed keys in place so surrounding
    /// comments and formatting are preserved.
    /// </summary>
    private static string UpsertSection (string text, Dictionary<string, string> entries)
    {
        // Find the active (non-commented) "EditorSettings" object.
        Match sectionMatch = Regex.Match (text, $@"(?<!//[^\n]*)""{SectionName}""\s*:\s*\{{");

        if (!sectionMatch.Success)
        {
            // No section yet — insert a full nested block before the last closing '}'.
            string block = $"  \"{SectionName}\": {{\n"
                           + string.Join (",\n", entries.Select (kvp => $"    \"{kvp.Key}\": {kvp.Value}"))
                           + "\n  }";

            return InsertBeforeLastBrace (text, block);
        }

        int openBrace = sectionMatch.Index + sectionMatch.Length - 1;
        int closeBrace = FindMatchingBrace (text, openBrace);

        if (closeBrace < 0)
        {
            // Malformed file — leave it untouched rather than corrupting it further.
            return text;
        }

        string body = text[openBrace..(closeBrace + 1)];
        List<string> toInsert = [];

        foreach (KeyValuePair<string, string> kvp in entries)
        {
            // Replace an existing key in-place (preserves surrounding JSONC).
            // The negative lookbehind skips keys inside JSONC line comments.
            // Only matches bool and int values (all current EditorSettings types).
            string pattern = $@"(?<!//[^\n]*)(""{Regex.Escape (kvp.Key)}""\s*:\s*)(?:true|false|-?\d+)";

            if (Regex.IsMatch (body, pattern))
            {
                body = Regex.Replace (body, pattern, $"${{1}}{kvp.Value}");
            }
            else
            {
                toInsert.Add ($"    \"{kvp.Key}\": {kvp.Value}");
            }
        }

        if (toInsert.Count > 0)
        {
            body = InsertBeforeLastBrace (body, string.Join (",\n", toInsert));
        }

        return text[..openBrace] + body + text[(closeBrace + 1)..];
    }

    /// <summary>
    /// Inserts <paramref name="block"/> before the last closing <c>'}'</c> in
    /// <paramref name="text"/>, adding a separating comma after the preceding
    /// JSON value when one is needed.
    /// </summary>
    private static string InsertBeforeLastBrace (string text, string block)
    {
        int lastBrace = text.LastIndexOf ('}');

        if (lastBrace < 0)
        {
            return text;
        }

        // Find the position of the last non-whitespace, non-comment character
        // before the closing brace so we can insert a comma after it.
        int insertCommaAfter = FindLastJsonTokenPosition (text, lastBrace);

        if (insertCommaAfter >= 0 && text[insertCommaAfter] != ',' && text[insertCommaAfter] != '{')
        {
            // Insert comma after the last JSON value
            text = text.Insert (insertCommaAfter + 1, ",");

            // Adjust lastBrace since we inserted a character
            lastBrace = text.LastIndexOf ('}');
        }

        return text.Insert (lastBrace, $"\n\n{block}\n");
    }

    /// <summary>
    /// Finds the index of the <c>'}'</c> matching the <c>'{'</c> at
    /// <paramref name="openBrace"/> by brace counting, skipping string literals
    /// and JSONC line comments. Returns -1 when unbalanced.
    /// </summary>
    private static int FindMatchingBrace (string text, int openBrace)
    {
        int depth = 0;
        bool inString = false;

        for (int i = openBrace; i < text.Length; i++)
        {
            char c = text[i];

            if (inString)
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;

                    break;
                case '/' when i + 1 < text.Length && text[i + 1] == '/':
                    // Skip to end of line comment.
                    int eol = text.IndexOf ('\n', i);
                    i = eol < 0 ? text.Length : eol;

                    break;
                case '{':
                    depth++;

                    break;
                case '}':
                    depth--;

                    if (depth == 0)
                    {
                        return i;
                    }

                    break;
            }
        }

        return -1;
    }

    /// <summary>
    /// Finds the position of the last non-whitespace, non-comment character
    /// before <paramref name="braceIndex"/>. This is where a trailing comma
    /// should be inserted when appending new properties.
    /// Returns -1 if only whitespace/comments precede the brace.
    /// </summary>
    private static int FindLastJsonTokenPosition (string text, int braceIndex)
    {
        int i = braceIndex - 1;

        while (i >= 0)
        {
            char c = text[i];

            if (char.IsWhiteSpace (c))
            {
                i--;

                continue;
            }

            // If this position lies inside a line comment (whole-line or trailing
            // after a value), skip to just before the comment's "//" marker so the
            // comma lands after the value, not inside the comment.
            int lineStart = text.LastIndexOf ('\n', i) + 1;
            int commentStart = FindLineCommentStart (text, lineStart, i);

            if (commentStart >= 0)
            {
                i = commentStart - 1;

                continue;
            }

            return i;
        }

        return -1;
    }

    /// <summary>
    /// Finds the absolute index of the first <c>//</c> comment marker on the line
    /// starting at <paramref name="lineStart"/>, ignoring markers inside string
    /// literals (e.g. URLs). Only markers at or before <paramref name="limit"/>
    /// count. Returns -1 when <paramref name="limit"/> is not inside a line comment.
    /// </summary>
    private static int FindLineCommentStart (string text, int lineStart, int limit)
    {
        bool inString = false;

        for (int j = lineStart; j <= limit; j++)
        {
            char c = text[j];

            if (inString)
            {
                if (c == '\\')
                {
                    j++;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;

                continue;
            }

            if (c == '/' && j + 1 < text.Length && text[j + 1] == '/')
            {
                return j;
            }
        }

        return -1;
    }
}
