using Terminal.Gui.App;
using Terminal.Gui.Configuration;

namespace Terminal.Gui.Cli;

/// <summary>
/// The main entry point. Owns parsing, dispatch, lifecycle, and output.
/// </summary>
public sealed class CliHost
{
    private readonly CliHostOptions _options;
    private readonly ArgParser _parser;

    public CliHost (Action<CliHostOptions>? configure = null)
    {
        _options = new CliHostOptions ();
        configure?.Invoke (_options);
        Registry = new CommandRegistry ();
        _parser = new ArgParser (_options.GlobalOptions, _options.MaxInitialChars);
    }

    /// <summary>The command registry. Register commands before calling RunAsync.</summary>
    public ICommandRegistry Registry { get; }

    /// <summary>Parse args, dispatch, run the TUI, format output, return exit code.</summary>
    public async Task<int> RunAsync (
        string[] args,
        CancellationToken cancellationToken = default,
        TextWriter? stdout = null,
        TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        // Intercept `<app> <alias> help|--help|-h` before parsing (avoids unknown-option error)
        if (args.Length >= 2
            && args[0] is not "--help" and not "-h" and not "--version" and not "--opencli"
            && args[1] is "help" or "--help" or "-h")
        {
            return await HandleCommandHelp (args[0], stdout, stderr);
        }

        ArgParser.ParseResult parsed = _parser.Parse (args);

        if (!parsed.Success)
        {
            await stderr.WriteLineAsync ($"error: {parsed.Error}");

            return ExitCodes.UsageError;
        }

        // Handle root flags
        if (parsed.RootFlag is not null)
        {
            return await HandleRootFlag (parsed.RootFlag.Value, stdout);
        }

        string alias = parsed.Alias!;

        if (!Registry.TryResolve (alias, out ICliCommand? command) || command is null)
        {
            await stderr.WriteLineAsync (
                $"error: unknown command '{alias}'. Try '{_options.ApplicationName} --help' to see available commands.");

            return ExitCodes.UsageError;
        }

        // Re-parse with command context for per-command option validation
        ArgParser.ParseResult fullParsed = _parser.Parse (args, command);

        if (!fullParsed.Success)
        {
            await stderr.WriteLineAsync ($"error: {fullParsed.Error}");

            return ExitCodes.UsageError;
        }

        CommandRunOptions options = fullParsed.Options!;

        // Validate positional args
        if (options.Arguments.Count > 0 && !command.AcceptsPositionalArgs)
        {
            string joined = string.Join (" ", options.Arguments);
            await stderr.WriteLineAsync ($"error: '{alias}' does not accept positional arguments: {joined}");

            return ExitCodes.UsageError;
        }

        // Validate --initial
        string? initial = options.Initial;

        if (initial is not null && !command.TryValidateInitial (initial, options))
        {
            await stderr.WriteLineAsync ($"error: invalid --initial value '{initial}' for '{alias}'.");

            return ExitCodes.UsageError;
        }

        // Build cancellation (user ct + timeout)
        using CancellationTokenSource? timeoutSource = options.Timeout is { } timeout
            ? new (timeout)
            : null;
        using CancellationTokenSource linkedSource = timeoutSource is null
            ? CancellationTokenSource.CreateLinkedTokenSource (cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, timeoutSource.Token);

        // --cat mode for viewer commands
        if (options.Cat && command is IViewerCommand viewer)
        {
            CommandResult? catResult = await viewer.RenderCatAsync (options, stdout, linkedSource.Token);

            if (catResult is { } cr)
            {
                if (!ResultWriter.Write (cr, options.JsonOutput, stdout, stderr, options.OutputPath))
                {
                    return ExitCodes.UsageError;
                }

                return ExitCodes.FromResult (cr);
            }
        }

        // Normal TUI dispatch
        CommandResult result;

        try
        {
            try
            {
                ConfigurationManager.Enable (ConfigLocations.All);
            }
            catch
            {
                ConfigurationManager.Disable (resetToHardCodedDefaults: true);
                ConfigurationManager.Enable (ConfigLocations.None);
            }

            bool useFullscreen = options.Fullscreen || command.Kind == CommandKind.Viewer;
            Application.AppModel = useFullscreen ? AppModel.FullScreen : AppModel.Inline;

            using IApplication app = Application.Create ();
            app.Init ();

            try
            {
                result = await command.RunAsync (app, initial, options, linkedSource.Token);
            }
            catch (OperationCanceledException)
            {
                result = new (CommandStatus.Cancelled, null, null, null);
            }
            catch (Exception ex)
            {
                result = new (CommandStatus.Error, null, "io", ex.Message);
            }
        }
        catch (Exception ex)
        {
            result = new (CommandStatus.Error, null, "io", $"Failed to initialize Terminal.Gui: {ex.Message}");
        }

        if (!ResultWriter.Write (result, options.JsonOutput, stdout, stderr, options.OutputPath))
        {
            return ExitCodes.UsageError;
        }

        return ExitCodes.FromResult (result);
    }

    private async Task<int> HandleRootFlag (ArgParser.RootFlag flag, TextWriter stdout)
    {
        switch (flag)
        {
            case ArgParser.RootFlag.Help:
                WriteRootHelp (stdout);

                return ExitCodes.Ok;

            case ArgParser.RootFlag.Version:
                string version = _options.Version ?? "0.0.0";
                await stdout.WriteLineAsync ($"{_options.ApplicationName} {version}");

                return ExitCodes.Ok;

            case ArgParser.RootFlag.OpenCli:
                string openCli = OpenCliWriter.Generate (Registry, _options);
                await stdout.WriteLineAsync (openCli);

                return ExitCodes.Ok;

            default:
                return ExitCodes.Ok;
        }
    }

    private void WriteRootHelp (TextWriter stdout)
    {
        IHelpProvider provider = _options.HelpProvider ?? new MetadataHelpProvider ();
        string? help = provider.GetRootHelp (Registry);

        if (help is not null)
        {
            stdout.Write (help);
        }
        else
        {
            stdout.WriteLine ($"{_options.ApplicationName} — CLI tool");
            stdout.WriteLine ();
            stdout.WriteLine ($"Usage: {_options.ApplicationName} <command> [options]");
        }
    }

    private async Task<int> HandleCommandHelp (string alias, TextWriter stdout, TextWriter stderr)
    {
        if (!Registry.TryResolve (alias, out ICliCommand? command) || command is null)
        {
            await stderr.WriteLineAsync ($"error: unknown command '{alias}'.");

            return ExitCodes.UsageError;
        }

        IHelpProvider provider = _options.HelpProvider ?? new MetadataHelpProvider ();
        string? help = provider.GetCommandHelp (command);

        if (help is not null)
        {
            await stdout.WriteAsync (help);
        }

        return ExitCodes.Ok;
    }
}
