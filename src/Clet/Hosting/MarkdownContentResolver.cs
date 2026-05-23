using Terminal.Gui.Cli;

namespace Clet;

/// <summary>
/// Resolves markdown content from file arguments, inline content, or stdin.
/// Shared by <c>MarkdownClet.RunAsync</c> and <c>AliasDispatcher.ResolveViewerContent</c> (--cat path).
/// Single source of truth for file-access semantics.
/// </summary>
internal static class MarkdownContentResolver
{
    /// <summary>Result of content resolution.</summary>
    internal readonly record struct ResolveResult
    {
        /// <summary>Resolved content string (may be null if resolution failed).</summary>
        public string? Content { get; init; }

        /// <summary>Ordered list of resolved file paths (empty if content came from inline or stdin).</summary>
        public List<string> Files { get; init; }

        /// <summary>Error code when resolution fails (null on success).</summary>
        public string? ErrorCode { get; init; }

        /// <summary>Error message when resolution fails (null on success).</summary>
        public string? ErrorMessage { get; init; }

        /// <summary>Whether content was resolved successfully.</summary>
        public bool IsSuccess => ErrorCode is null;
    }

    /// <summary>8 M character cap on stdin content to prevent OOM from untrusted piped input.</summary>
    internal const int MaxStdinChars = 8 * 1024 * 1024;

    /// <summary>
    /// Resolves markdown content using the standard priority: file args → inline content → stdin.
    /// </summary>
    /// <param name="inlineContent">Inline content (e.g. --initial value). May be null.</param>
    /// <param name="options">Run options containing arguments, allowed files, etc.</param>
    /// <param name="stdinReader">
    /// Reader for stdin content. Pass <c>Console.In</c> in production,
    /// or a <see cref="StringReader"/> in tests. Pass <c>null</c> if stdin is not redirected.
    /// </param>
    public static ResolveResult Resolve (string? inlineContent, CommandRunOptions options, TextReader? stdinReader)
    {
        // Priority 1: File arguments (with glob expansion)
        if (options.Arguments is { Count: > 0 } args)
        {
            IReadOnlyList<string> allowedFiles = options.GetExtensionList ("allow-file");
            FileAccessPolicy policy = new (
                Directory.GetCurrentDirectory (),
                FileAccessPolicy.MergeWithConfigPaths (allowedFiles.Count > 0 ? allowedFiles : null),
                options.HasExtension ("allow-binary"));

            List<string> files = ExpandFiles (args, policy, out string? policyError);

            if (policyError is not null)
            {
                return new () { Files = [], ErrorCode = "file-access-denied", ErrorMessage = policyError };
            }

            if (files.Count == 0)
            {
                return new () { Files = [], ErrorCode = "io", ErrorMessage = "No matching files found." };
            }

            // Read and concatenate file contents
            List<string> contents = [];
            long aggregateSize = 0;

            foreach (string file in files)
            {
                FileInfo fi = new (file);
                aggregateSize += fi.Length;

                if (aggregateSize > FileAccessPolicy.MaxAggregateSizeBytes)
                {
                    return new ()
                    {
                        Files = [],
                        ErrorCode = "input-too-large",
                        ErrorMessage = $"Refused: aggregate file size exceeds {FileAccessPolicy.MaxAggregateSizeBytes / (1024 * 1024)} MiB limit.",
                    };
                }

                try
                {
                    contents.Add (File.ReadAllText (file));
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.Error.WriteLine ($"Warning: Could not read file '{file}': {ex.Message}");
                }
                catch (IOException ex)
                {
                    Console.Error.WriteLine ($"Warning: Could not read file '{file}': {ex.Message}");
                }
            }

            string? joined = contents.Count > 0 ? string.Join ("\n\n", contents) : null;

            return new () { Content = joined, Files = files };
        }

        // Priority 2: Inline content
        if (!string.IsNullOrEmpty (inlineContent))
        {
            return new () { Content = inlineContent, Files = [] };
        }

        // Priority 3: Stdin
        if (stdinReader is not null)
        {
            char[] buffer = new char[MaxStdinChars + 1];
            int totalRead = 0;
            int charsRead;

            while (totalRead <= MaxStdinChars
                   && (charsRead = stdinReader.Read (buffer, totalRead, buffer.Length - totalRead)) > 0)
            {
                totalRead += charsRead;
            }

            if (totalRead > MaxStdinChars)
            {
                return new () { Files = [], ErrorCode = "input-too-large", ErrorMessage = "stdin exceeds the 8 M character limit." };
            }

            string stdinContent = new (buffer, 0, totalRead);

            if (string.IsNullOrEmpty (stdinContent))
            {
                return new () { Files = [], ErrorCode = "io", ErrorMessage = "No input received from stdin." };
            }

            return new () { Content = stdinContent, Files = [] };
        }

        // No content source found
        return new () { Files = [], ErrorCode = "io", ErrorMessage = "No file specified. Usage: clet md <file.md>" };
    }

    /// <summary>
    /// Expands file arguments (including glob patterns) and validates against the file access policy.
    /// </summary>
    internal static List<string> ExpandFiles (IReadOnlyList<string> patterns, FileAccessPolicy policy, out string? error)
    {
        List<string> result = [];
        error = null;

        foreach (string pattern in patterns)
        {
            if (pattern.Contains ('*') || pattern.Contains ('?'))
            {
                string directory = Path.GetDirectoryName (pattern) is { Length: > 0 } dir ? dir : ".";
                string filePattern = Path.GetFileName (pattern);

                if (Directory.Exists (directory))
                {
                    string[] matched = Directory.GetFiles (directory, filePattern);
                    string? globError = policy.CheckGlobAggregate (matched);

                    if (globError is not null)
                    {
                        error = globError;

                        return [];
                    }

                    foreach (string file in matched)
                    {
                        string? violation = policy.CheckFile (file);

                        if (violation is not null)
                        {
                            error = violation;

                            return [];
                        }

                        result.Add (Path.GetFullPath (file));
                    }
                }
            }
            else if (File.Exists (pattern))
            {
                string? violation = policy.CheckFile (pattern);

                if (violation is not null)
                {
                    error = violation;

                    return [];
                }

                result.Add (Path.GetFullPath (pattern));
            }
            else
            {
                Console.Error.WriteLine ($"Warning: File not found: {pattern}");
            }
        }

        return result;
    }
}
