using Terminal.Gui.Cli;

namespace Clet;

internal static class Program
{
    public static async Task<int> Main (string[] args)
    {
        CletLogging.Initialize ();
        CliHostOptions parserOptions = CreateOptions ();

        CliHost host = new (ConfigureOptions);

        BuiltInCommands.RegisterAll (host.Registry);

        if (TryHandleOversizedInitial (args, host.Registry, parserOptions, out int exitCode))
        {
            return exitCode;
        }

        using StringWriter stdout = new ();
        using StringWriter stderr = new ();

        int packageExitCode = await host.RunAsync (args, stdout: stdout, stderr: stderr);
        string stdoutText = stdout.ToString ();
        string stderrText = stderr.ToString ();

        Console.Out.Write (stdoutText);
        Console.Error.Write (stderrText);

        return CletExitCodes.MapPackageExit (packageExitCode, stdoutText);
    }

    private static bool TryHandleOversizedInitial (
        string[] args,
        ICommandRegistry registry,
        CliHostOptions options,
        out int exitCode)
    {
        exitCode = ExitCodes.UsageError;
        ArgParser parser = new (options.GlobalOptions, int.MaxValue);
        ArgParser.ParseResult rootParse = parser.Parse (args);

        if (!rootParse.Success
            || rootParse.RootFlag is not null
            || rootParse.Alias is null
            || !registry.TryResolve (rootParse.Alias, out _))
        {
            return false;
        }

        CommandRunOptions? runOptions = rootParse.Options;

        if (runOptions is null
            || runOptions.Initial is null
            || runOptions.Initial.Length <= options.MaxInitialChars)
        {
            return false;
        }

        CommandResult result = new (
            CommandStatus.Error,
            null,
            "input-too-large",
            $"--initial exceeds the maximum length of {options.MaxInitialChars} characters.");

        ResultWriter.Write (result, runOptions.JsonOutput, Console.Out, Console.Error, runOptions.OutputPath);
        exitCode = CletExitCodes.FromResult (result);

        return true;
    }

    private static CliHostOptions CreateOptions ()
    {
        CliHostOptions options = new ();
        ConfigureOptions (options);

        return options;
    }

    private static void ConfigureOptions (CliHostOptions options)
    {
        options.ApplicationName = "clet";
        options.Version = VersionInfo.GetCletVersion ();
        options.HelpProvider = new CletHelpProvider ();
        options.ResourceAssembly = typeof (Program).Assembly;

        // clet-specific global options
        options.GlobalOptions.Add (new GlobalOptionDescriptor ("allow-file", null,
            "Explicitly allow reading a file path (bypasses extension + cwd checks).", false, Repeatable: true));
        options.GlobalOptions.Add (new GlobalOptionDescriptor ("allow-binary", null,
            "Permit binary file content (NUL bytes).", true));
        options.GlobalOptions.Add (new GlobalOptionDescriptor ("no-browse", null,
            "Disable browser-mode navigation for viewer clets.", true));
    }
}
