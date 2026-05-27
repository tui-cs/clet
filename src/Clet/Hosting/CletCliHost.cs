using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.Time;

namespace Clet;

internal sealed class CletCliHost
{
    private readonly IHelpProvider _helpProvider;
    private readonly int _maxInitialChars;
    private readonly CliHostOptions _options;
    private readonly ArgParser _parser;

    public CletCliHost (Action<CliHostOptions>? configure = null)
    {
        _options = new ();
        configure?.Invoke (_options);

        _helpProvider = _options.HelpProvider ?? new MetadataHelpProvider ();
        _maxInitialChars = _options.MaxInitialChars;
        Registry = new CommandRegistry ();
        RegisterBuiltIns ();

        // Parse without the package's --initial cap so clet can map known-command
        // oversized input through its own validation error path instead of usage.
        _parser = new (_options.GlobalOptions, int.MaxValue);
    }

    public ICommandRegistry Registry { get; }

    public async Task<int> RunAsync (
        string[] args,
        CancellationToken cancellationToken = default,
        TextWriter? stdout = null,
        TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        ArgParser.ParseResult parseResult = _parser.Parse (args);

        if (!parseResult.Success)
        {
            stderr.WriteLine (parseResult.Error);

            return ExitCodes.UsageError;
        }

        if (parseResult.RootFlag is { } rootFlag)
        {
            WriteRootFlag (rootFlag, stdout);

            return ExitCodes.Ok;
        }

        if (parseResult.Alias is null
            || !Registry.TryResolve (parseResult.Alias, out ICliCommand? command)
            || command is null)
        {
            stderr.WriteLine ($"Unknown command '{parseResult.Alias}'.");

            return ExitCodes.UsageError;
        }

        if (parseResult.Options is not null
            && parseResult.Initial is { } initial
            && initial.Length > _maxInitialChars)
        {
            return WriteInputTooLargeResult (parseResult.Options, stdout, stderr);
        }

        return await DispatchCommandAsync (args, command, cancellationToken, stdout, stderr);
    }

    private async Task<int> DispatchCommandAsync (
        string[] args,
        ICliCommand command,
        CancellationToken cancellationToken,
        TextWriter stdout,
        TextWriter stderr)
    {
        ArgParser.ParseResult parseResult = _parser.Parse (args, command);

        if (!parseResult.Success || parseResult.Options is null)
        {
            stderr.WriteLine (parseResult.Error);

            return ExitCodes.UsageError;
        }

        return await ExecuteCommandAsync (command, parseResult.Options, cancellationToken, stdout, stderr);
    }

    private async Task<int> ExecuteCommandAsync (
        ICliCommand command,
        CommandRunOptions runOptions,
        CancellationToken cancellationToken,
        TextWriter stdout,
        TextWriter stderr)
    {
        if (runOptions.Initial is { } initial && initial.Length > _maxInitialChars)
        {
            return WriteInputTooLargeResult (runOptions, stdout, stderr);
        }

        if (runOptions.Initial is not null && !command.TryValidateInitial (runOptions.Initial, runOptions))
        {
            stderr.WriteLine ("Invalid --initial value.");

            return ExitCodes.ValidationError;
        }

        using CancellationTokenSource? timeoutSource = runOptions.Timeout.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource (cancellationToken)
            : null;

        if (timeoutSource is not null)
        {
            timeoutSource.CancelAfter (runOptions.Timeout.GetValueOrDefault ());
        }

        CancellationToken effectiveToken = timeoutSource?.Token ?? cancellationToken;

        if (command is IViewerCommand viewerCommand && runOptions.Cat)
        {
            CommandResult? commandResult;

            try
            {
                commandResult = await viewerCommand.RenderCatAsync (runOptions, stdout, effectiveToken);
            }
            catch (OperationCanceledException)
            {
                return ExitCodes.Cancelled;
            }

            if (commandResult.HasValue)
            {
                return CletExitCodes.FromResult (commandResult.Value);
            }
        }

        CommandResult runResult;

        try
        {
            runResult = await RunWithTerminalGuiAsync (command, runOptions, effectiveToken);
        }
        catch (OperationCanceledException)
        {
            runResult = new (CommandStatus.Cancelled, null, null, null);
        }

        if (!ResultWriter.Write (runResult, runOptions.JsonOutput, stdout, stderr, runOptions.OutputPath))
        {
            return ExitCodes.UsageError;
        }

        return CletExitCodes.FromResult (runResult);
    }

    private int WriteInputTooLargeResult (CommandRunOptions runOptions, TextWriter stdout, TextWriter stderr)
    {
        CommandResult result = new (
            CommandStatus.Error,
            null,
            "input-too-large",
            $"--initial exceeds the maximum length of {_maxInitialChars} characters.");

        ResultWriter.Write (result, runOptions.JsonOutput, stdout, stderr, runOptions.OutputPath);

        return CletExitCodes.FromResult (result);
    }

    private async Task<CommandResult> RunWithTerminalGuiAsync (
        ICliCommand command,
        CommandRunOptions runOptions,
        CancellationToken cancellationToken)
    {
        IApplication app = Application.Create ((ITimeProvider?)null).Init ((string?)null);

        try
        {
            return await command.RunAsync (app, runOptions.Initial, runOptions, cancellationToken);
        }
        finally
        {
            (app as IDisposable)?.Dispose ();
        }
    }

    private void WriteRootFlag (ArgParser.RootFlag rootFlag, TextWriter stdout)
    {
        switch (rootFlag)
        {
            case ArgParser.RootFlag.Help:
                MarkdownRenderer.RenderToAnsi (
                    _helpProvider.GetRootHelp (Registry)
                    ?? new MetadataHelpProvider ().GetRootHelp (Registry)
                    ?? string.Empty,
                    stdout);

                break;
            case ArgParser.RootFlag.Version:
                stdout.WriteLine ($"{_options.ApplicationName} {_options.Version ?? "0.0.0"}");

                break;
            case ArgParser.RootFlag.OpenCli:
                stdout.WriteLine (OpenCliWriter.Generate (Registry, _options));

                break;
        }
    }

    private void RegisterBuiltIns ()
    {
        Registry.Register (new HelpCommand (Registry, _helpProvider));
    }
}
