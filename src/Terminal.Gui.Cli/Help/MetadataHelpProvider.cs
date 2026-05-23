using System.Globalization;
using System.Text;

namespace Terminal.Gui.Cli;

/// <summary>
/// Generates help text from registry metadata when no custom IHelpProvider is configured.
/// </summary>
public sealed class MetadataHelpProvider : IHelpProvider
{
    public string? GetRootHelp (ICommandRegistry registry)
    {
        StringBuilder sb = new ();
        sb.AppendLine ("Commands:");
        sb.AppendLine ();

        int maxWidth = 0;

        foreach (ICliCommand cmd in registry.All)
        {
            if (cmd.PrimaryAlias.Length > maxWidth)
            {
                maxWidth = cmd.PrimaryAlias.Length;
            }
        }

        foreach (ICliCommand cmd in registry.All)
        {
            sb.Append ("  ");
            sb.Append (cmd.PrimaryAlias.PadRight (maxWidth + 2));
            sb.AppendLine (cmd.Description);
        }

        return sb.ToString ();
    }

    public string? GetCommandHelp (ICliCommand command)
    {
        StringBuilder sb = new ();
        sb.AppendLine (command.Description);
        sb.AppendLine ();
        sb.Append ("Aliases: ");
        sb.AppendLine (string.Join (", ", command.Aliases));

        if (command.Options.Count > 0)
        {
            sb.AppendLine ();
            sb.AppendLine ("Options:");

            foreach (CommandOptionDescriptor opt in command.Options)
            {
                sb.Append ("  --");
                sb.Append (opt.Name);

                if (opt.ShortName is not null)
                {
                    sb.Append (CultureInfo.InvariantCulture, $", -{opt.ShortName}");
                }

                sb.Append ("  ");
                sb.AppendLine (opt.Description);
            }
        }

        return sb.ToString ();
    }
}
