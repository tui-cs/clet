using Terminal.Gui.Cli;

namespace Clet;

internal static class Program
{
    public static async Task<int> Main (string[] args)
    {
        CletLogging.Initialize ();
        int maxInitialChars = new CliHostOptions ().MaxInitialChars;

        CliHost host = new (options =>
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
        });

        BuiltInCommands.RegisterAll (host.Registry);

        if (TryHandleOversizedInitial (args, host.Registry, maxInitialChars, out int exitCode))
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
        int maxInitialChars,
        out int exitCode)
    {
        exitCode = ExitCodes.UsageError;
        string? alias = null;
        string? initial = null;
        bool jsonOutput = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            switch (arg)
            {
                case "--json":
                    jsonOutput = true;

                    continue;
                case "--initial" when i + 1 < args.Length:
                    initial = args[++i];

                    continue;
                case "--initial":
                    return false;
            }

            if (alias is null)
            {
                if (arg.StartsWith ('-'))
                {
                    if (OptionConsumesValue (arg))
                    {
                        i++;
                    }

                    continue;
                }

                alias = arg;
            }
        }

        if (alias is null
            || initial is null
            || initial.Length <= maxInitialChars
            || !registry.TryResolve (alias, out _))
        {
            return false;
        }

        CommandResult result = new (
            CommandStatus.Error,
            null,
            "input-too-large",
            $"--initial exceeds the maximum length of {maxInitialChars} characters.");

        ResultWriter.Write (result, jsonOutput, Console.Out, Console.Error);
        exitCode = CletExitCodes.FromResult (result);

        return true;
    }

    private static bool OptionConsumesValue (string arg) =>
        arg is "--prompt" or "--title" or "-p" or "-t" or "--timeout" or "--output" or "-o" or "--rows" or "--allow-file";
}
