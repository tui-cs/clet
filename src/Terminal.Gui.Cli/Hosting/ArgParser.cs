using System.Globalization;

namespace Terminal.Gui.Cli;

/// <summary>
/// Data-driven argument parser. Recognizes framework-owned flags, consumer-registered
/// global options, and per-command options. Rejects unknown options (exit 2).
/// </summary>
public sealed class ArgParser
{
    private readonly List<GlobalOptionDescriptor> _globalOptions;
    private readonly int _maxInitialChars;

    public ArgParser (List<GlobalOptionDescriptor> globalOptions, int maxInitialChars = 64 * 1024)
    {
        _globalOptions = globalOptions;
        _maxInitialChars = maxInitialChars;
    }

    /// <summary>Represents the result of parsing arguments.</summary>
    public sealed class ParseResult
    {
        /// <summary>True if parsing succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>Error message when Success is false.</summary>
        public string? Error { get; init; }

        /// <summary>The command alias (first non-option argument).</summary>
        public string? Alias { get; init; }

        /// <summary>The initial value (--initial).</summary>
        public string? Initial { get; init; }

        /// <summary>Parsed options bag.</summary>
        public CommandRunOptions? Options { get; init; }

        /// <summary>Root flag detected (--help, --version, --opencli).</summary>
        public RootFlag? RootFlag { get; init; }

        public static ParseResult Fail (string error) => new () { Success = false, Error = error };
    }

    public enum RootFlag { Help, Version, OpenCli }

    /// <summary>
    /// Parses command-line arguments. The <paramref name="command"/> is used to validate
    /// per-command options. Pass null when the command is not yet resolved (pre-dispatch validation).
    /// </summary>
    public ParseResult Parse (string[] args, ICliCommand? command = null)
    {
        if (args.Length == 0)
        {
            return new () { Success = true, RootFlag = RootFlag.Help };
        }

        // Check root flags first
        if (args[0] is "--help" or "-h")
        {
            return new () { Success = true, RootFlag = RootFlag.Help };
        }

        if (args[0] == "--version")
        {
            return new () { Success = true, RootFlag = RootFlag.Version };
        }

        if (args[0] == "--opencli")
        {
            return new () { Success = true, RootFlag = RootFlag.OpenCli };
        }

        string alias = args[0];

        // Parse remaining args
        string? initial = null;
        string? title = null;
        string? outputPath = null;
        bool jsonOutput = false;
        bool fullscreen = false;
        bool cat = false;
        TimeSpan? timeout = null;
        int? rows = null;
        Dictionary<string, string> commandOptions = new (StringComparer.OrdinalIgnoreCase);
        List<string> positionalArgs = [];
        Dictionary<string, List<string>> extensions = new (StringComparer.OrdinalIgnoreCase);
        bool endOfOptions = false;

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];

            // After --, everything is positional
            if (endOfOptions)
            {
                positionalArgs.Add (arg);

                continue;
            }

            if (arg == "--")
            {
                endOfOptions = true;

                continue;
            }

            // Try --option=value split
            string optionName = arg;
            string? inlineValue = null;

            if (arg.StartsWith ("--", StringComparison.Ordinal) && arg.Contains ('='))
            {
                int eqIdx = arg.IndexOf ('=');
                optionName = arg[..eqIdx];
                inlineValue = arg[(eqIdx + 1)..];
            }

            // Framework-owned flags
            if (optionName is "--json" or "-j")
            {
                jsonOutput = true;

                continue;
            }

            if (optionName is "--fullscreen" or "-f")
            {
                fullscreen = true;

                continue;
            }

            if (optionName == "--cat")
            {
                cat = true;

                continue;
            }

            if (optionName is "--initial" or "-i")
            {
                string? value = inlineValue ?? ConsumeNextValue (args, ref i, command);

                if (value is null)
                {
                    return ParseResult.Fail ("--initial requires a value.");
                }

                initial = value;

                continue;
            }

            if (optionName is "--title" or "-t" or "--prompt" or "-p")
            {
                string? value = inlineValue ?? ConsumeNextValue (args, ref i, command);

                if (value is null)
                {
                    return ParseResult.Fail ("--title requires a value.");
                }

                title = value;

                continue;
            }

            if (optionName == "--timeout")
            {
                string? value = inlineValue ?? ConsumeNextValue (args, ref i, command);

                if (value is null)
                {
                    return ParseResult.Fail ("--timeout requires a value (e.g. 30s, 500ms).");
                }

                if (!TryParseTimeout (value, out TimeSpan parsed))
                {
                    return ParseResult.Fail ($"invalid --timeout value '{value}'. Use 30s, 1m, 500ms.");
                }

                timeout = parsed;

                continue;
            }

            if (optionName is "--output" or "-o")
            {
                string? value = inlineValue ?? ConsumeNextValue (args, ref i, command);

                if (value is null)
                {
                    return ParseResult.Fail ("--output requires a file path.");
                }

                outputPath = value;

                continue;
            }

            if (optionName is "--rows" or "-r")
            {
                string? value = inlineValue ?? ConsumeNextValue (args, ref i, command);

                if (value is null)
                {
                    return ParseResult.Fail ("--rows requires a value.");
                }

                if (!int.TryParse (value, out int parsedRows) || parsedRows < 1)
                {
                    return ParseResult.Fail ($"invalid --rows value '{value}'. Must be a positive integer.");
                }

                rows = parsedRows;

                continue;
            }

            // Consumer-registered global options
            if (TryMatchGlobalOption (optionName, out GlobalOptionDescriptor? globalOpt))
            {
                if (globalOpt!.IsFlag)
                {
                    if (!extensions.TryGetValue (globalOpt.Name, out List<string>? list))
                    {
                        list = [];
                        extensions[globalOpt.Name] = list;
                    }

                    list.Add (string.Empty);
                }
                else
                {
                    string? value = inlineValue ?? ConsumeNextValue (args, ref i, command);

                    if (value is null)
                    {
                        return ParseResult.Fail ($"--{globalOpt.Name} requires a value.");
                    }

                    if (!extensions.TryGetValue (globalOpt.Name, out List<string>? list))
                    {
                        list = [];
                        extensions[globalOpt.Name] = list;
                    }

                    if (globalOpt.Repeatable)
                    {
                        list.Add (value);
                    }
                    else
                    {
                        list.Clear ();
                        list.Add (value);
                    }
                }

                continue;
            }

            // Per-command options (--name value)
            if (arg.StartsWith ("--", StringComparison.Ordinal) || (arg.Length == 2 && arg[0] == '-'))
            {
                string cmdOptName = optionName.StartsWith ("--", StringComparison.Ordinal)
                    ? optionName[2..]
                    : optionName[1..];

                if (command is not null && !IsCommandOption (command, cmdOptName))
                {
                    return ParseResult.Fail ($"unknown option '{arg}' for '{alias}'.");
                }

                // Even if command is null (pre-validation), treat unrecognized -- args as command options
                if (command is null && !arg.StartsWith ("--", StringComparison.Ordinal))
                {
                    // Short option that doesn't match anything — could be positional
                    positionalArgs.Add (arg);

                    continue;
                }

                string? value = inlineValue ?? ConsumeNextValue (args, ref i, command);

                if (value is null)
                {
                    return ParseResult.Fail ($"option '{arg}' requires a value.");
                }

                commandOptions[cmdOptName] = value;

                continue;
            }

            // Positional argument
            positionalArgs.Add (arg);
        }

        // Validate --initial size
        if (initial is not null && initial.Length > _maxInitialChars)
        {
            return ParseResult.Fail (
                $"--initial value exceeds the {_maxInitialChars / 1024}K character limit ({initial.Length} characters).");
        }

        // Build readonly extensions dictionary
        Dictionary<string, IReadOnlyList<string>> readOnlyExtensions = new (StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, List<string>> kvp in extensions)
        {
            readOnlyExtensions[kvp.Key] = kvp.Value.AsReadOnly ();
        }

        CommandRunOptions options = new ()
        {
            Initial = initial,
            Title = title,
            JsonOutput = jsonOutput,
            Timeout = timeout,
            Fullscreen = fullscreen,
            Cat = cat,
            OutputPath = outputPath,
            Rows = rows,
            Arguments = positionalArgs,
            CommandOptions = commandOptions,
            Extensions = readOnlyExtensions,
        };

        return new ()
        {
            Success = true,
            Alias = alias,
            Initial = initial,
            Options = options,
        };
    }

    private bool TryMatchGlobalOption (string token, out GlobalOptionDescriptor? match)
    {
        match = null;
        string name;

        if (token.StartsWith ("--", StringComparison.Ordinal))
        {
            name = token[2..];
        }
        else if (token.Length == 2 && token[0] == '-')
        {
            name = token[1..];

            // Match by short name
            foreach (GlobalOptionDescriptor opt in _globalOptions)
            {
                if (string.Equals (opt.ShortName, name, StringComparison.OrdinalIgnoreCase))
                {
                    match = opt;

                    return true;
                }
            }

            return false;
        }
        else
        {
            return false;
        }

        foreach (GlobalOptionDescriptor opt in _globalOptions)
        {
            if (string.Equals (opt.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                match = opt;

                return true;
            }
        }

        return false;
    }

    private static bool IsCommandOption (ICliCommand command, string name)
    {
        foreach (CommandOptionDescriptor opt in command.Options)
        {
            if (string.Equals (opt.Name, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals (opt.ShortName, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string? ConsumeNextValue (string[] args, ref int i, ICliCommand? command)
    {
        if (i + 1 >= args.Length)
        {
            return null;
        }

        string candidate = args[i + 1];

        // Don't consume if the next arg looks like an option
        if (IsOptionToken (candidate, command))
        {
            return null;
        }

        i++;

        return candidate;
    }

    private bool IsOptionToken (string token, ICliCommand? command)
    {
        if (token == "--")
        {
            return true;
        }

        if (token is "--json" or "-j" or "--fullscreen" or "-f" or "--cat"
            or "--timeout" or "--initial" or "-i" or "--title" or "-t"
            or "--prompt" or "-p" or "--output" or "-o" or "--rows" or "-r" or "--opencli")
        {
            return true;
        }

        if (token.StartsWith ("--", StringComparison.Ordinal))
        {
            string name = token.Contains ('=') ? token[2..token.IndexOf ('=')] : token[2..];

            // Check consumer globals
            foreach (GlobalOptionDescriptor opt in _globalOptions)
            {
                if (string.Equals (opt.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Check command options
            if (command is not null)
            {
                foreach (CommandOptionDescriptor opt in command.Options)
                {
                    if (string.Equals (opt.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        if (token.Length == 2 && token[0] == '-')
        {
            string shortName = token[1..];

            foreach (GlobalOptionDescriptor opt in _globalOptions)
            {
                if (string.Equals (opt.ShortName, shortName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (command is not null)
            {
                foreach (CommandOptionDescriptor opt in command.Options)
                {
                    if (string.Equals (opt.ShortName, shortName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Parse timeout duration strings: 30s, 500ms, 1m, 1h.</summary>
    public static bool TryParseTimeout (string input, out TimeSpan timeout)
    {
        timeout = TimeSpan.Zero;

        if (string.IsNullOrEmpty (input))
        {
            return false;
        }

        string body;
        Func<double, TimeSpan> factory;

        if (input.EndsWith ("ms", StringComparison.OrdinalIgnoreCase))
        {
            body = input[..^2];
            factory = TimeSpan.FromMilliseconds;
        }
        else if (input.EndsWith ("s", StringComparison.OrdinalIgnoreCase))
        {
            body = input[..^1];
            factory = TimeSpan.FromSeconds;
        }
        else if (input.EndsWith ("m", StringComparison.OrdinalIgnoreCase))
        {
            body = input[..^1];
            factory = TimeSpan.FromMinutes;
        }
        else if (input.EndsWith ("h", StringComparison.OrdinalIgnoreCase))
        {
            body = input[..^1];
            factory = TimeSpan.FromHours;
        }
        else
        {
            return false;
        }

        if (!double.TryParse (body, NumberStyles.Number, CultureInfo.InvariantCulture, out double value) || value <= 0)
        {
            return false;
        }

        timeout = factory (value);

        return true;
    }
}
