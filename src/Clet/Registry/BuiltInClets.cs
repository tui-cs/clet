using Terminal.Gui.Cli;

namespace Clet;

internal static class BuiltInClets
{
    public static void RegisterAll (ICommandRegistry registry)
    {
        registry.Register (new SelectClet ());
        registry.Register (new TextClet ());
        registry.Register (new IntClet ());
        registry.Register (new DecimalClet ());
        registry.Register (new ConfirmClet ());
        registry.Register (new DateClet ());
        registry.Register (new TimeClet ());
        registry.Register (new DurationClet ());
        registry.Register (new ColorClet ());
        registry.Register (new MultiSelectClet ());
        registry.Register (new AttributePickerClet ());
        registry.Register (new PickFileClet ());
        registry.Register (new PickDirectoryClet ());
        registry.Register (new LinearRangeClet ());
        registry.Register (new EditorClet ());
        registry.Register (new MarkdownClet ());
        registry.Register (new ConfigClet ());
        registry.Register (new HelpClet (registry));
    }
}
