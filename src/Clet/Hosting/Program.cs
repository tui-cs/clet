using Terminal.Gui.Cli;

namespace Clet;

internal static class Program
{
    public static async Task<int> Main (string[] args)
    {
        CletLogging.Initialize ();

        CletCliHost host = new (options =>
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

        return await host.RunAsync (args);
    }
}
