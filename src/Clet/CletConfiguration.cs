using Microsoft.Extensions.Configuration;
using Terminal.Gui.Configuration;

namespace Clet;

/// <summary>
/// Central entry point for clet's Terminal.Gui configuration (MEC-based).
/// Wraps a <see cref="TuiConfigurationBuilder"/> pinned to the "clet" app name so
/// <c>~/.tui/clet.config.json</c> and <c>./.tui/clet.config.json</c> are discovered
/// even when the entry assembly is not the clet binary (e.g. test hosts).
/// </summary>
internal static class CletConfiguration
{
    /// <summary>The builder that aggregates all configuration sources for clet.</summary>
    internal static TuiConfigurationBuilder Builder { get; } = new ("clet");

    /// <summary>
    /// Applies Terminal.Gui settings from all sources to the static facades, then
    /// binds clet's own sections (<c>EditorSettings</c>, <c>FileAccessSettings</c>).
    /// Call once at startup and after <see cref="Reload"/>.
    /// </summary>
    internal static void Apply ()
    {
        Builder.ApplyToStaticFacades ();
        LoadCletSections ();
    }

    /// <summary>
    /// Rebuilds the configuration from all sources (after a config file changed on
    /// disk) and re-applies it. Also reloads <see cref="TuiConfigurationBuilder.Shared"/>
    /// so static facades backed by it (e.g. <see cref="ThemeManager"/> theme names)
    /// see the change.
    /// </summary>
    internal static void Reload ()
    {
        TuiConfigurationBuilder.Shared.Reload ();
        Builder.Reload ();
        Apply ();
    }

    /// <summary>Binds clet's own configuration sections onto their static settings classes.</summary>
    private static void LoadCletSections ()
    {
        IConfiguration config = Builder.Configuration;
        EditorSettings.Load (config.GetSection (EditorSettings.SectionName));
        FileAccessSettings.Load (config.GetSection (FileAccessSettings.SectionName));
    }
}
