using Terminal.Gui.App;
using Terminal.Gui.Cli;

namespace Clet;

internal static class Program
{
    public static async Task<int> Main (string[] args)
    {
        CletLogging.Initialize ();
        Logging.Information ($"clet starting with args: [{string.Join (", ", args)}]");

        using CancellationTokenSource cts = new ();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel ();
        };

        CliHost host = new (o =>
        {
            o.ApplicationName = "clet";
            o.Version = $"{VersionInfo.GetCletVersion ()} (Terminal.Gui {VersionInfo.GetTerminalGuiVersion ()})";
            o.GlobalOptions.Add (new ("allow-file", null, "Permit file access outside cwd", IsFlag: false, Repeatable: true));
            o.GlobalOptions.Add (new ("allow-binary", null, "Permit binary file content", IsFlag: true));
            o.GlobalOptions.Add (new ("no-browse", null, "Disable link navigation in viewers", IsFlag: true));
        });

        BuiltInClets.RegisterAll (host.Registry);

        return await host.RunAsync (args, cts.Token);
    }
}
