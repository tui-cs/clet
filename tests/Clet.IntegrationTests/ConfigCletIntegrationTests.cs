using Terminal.Gui.App;
using Xunit;
using Terminal.Gui.Cli;

namespace Clet.IntegrationTests;

public class ConfigCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        ConfigClet clet = new ();
        CommandRunOptions options = new ();

        using CancellationTokenSource cts = new ();
        await cts.CancelAsync ();

        CommandResult result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CommandStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithBadThemeInConfig_DoesNotThrow ()
    {
        // Create a temp config file with a bad theme name
        string tempDir = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ():N}");
        string tuiDir = Path.Combine (tempDir, ".tui");
        Directory.CreateDirectory (tuiDir);
        string configPath = Path.Combine (tuiDir, "clet.config.json");

        await File.WriteAllTextAsync (configPath, """
            {
              "$schema": "https://gui-cs.github.io/Terminal.Gui/schemas/tui-config-schema.json",
              "Theme": "Andersx"
            }
            """, TestContext.Current.CancellationToken);

        try
        {
            // Validate config returns an error message (doesn't throw)
            string? error = ConfigClet.ValidateConfig (configPath);
            Assert.NotNull (error);
        }
        finally
        {
            Directory.Delete (tempDir, true);
        }
    }

    [Fact]
    public async Task RunAsync_WithBadTheme_StopAfterFirstIteration_ReturnsOk ()
    {
        // Create a temp config with bad theme
        string tempDir = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ():N}");
        string tuiDir = Path.Combine (tempDir, ".tui");
        Directory.CreateDirectory (tuiDir);
        string configPath = Path.Combine (tuiDir, "clet.config.json");

        await File.WriteAllTextAsync (configPath, """
            {
              "$schema": "https://gui-cs.github.io/Terminal.Gui/schemas/tui-config-schema.json",
              "Theme": "Andersx"
            }
            """, TestContext.Current.CancellationToken);

        try
        {
            using IApplication app = Application.Create ();
            app.Init ("ansi");
            app.StopAfterFirstIteration = true;

            ConfigClet clet = new ();
            CommandRunOptions options = new ();

            using CancellationTokenSource cts = new ();

            // This should NOT throw — errors should be caught internally
            CommandResult result = await clet.RunAsync (app, null, options, cts.Token);

            Assert.Equal (CommandStatus.Ok, result.Status);
        }
        finally
        {
            Directory.Delete (tempDir, true);
        }
    }
}

