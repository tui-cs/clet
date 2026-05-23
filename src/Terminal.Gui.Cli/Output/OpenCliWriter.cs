using System.Globalization;
using System.Text;

namespace Terminal.Gui.Cli;

/// <summary>
/// Generates an OpenCLI JSON document from registry metadata.
/// Hand-built JSON for AOT-friendliness.
/// </summary>
public static class OpenCliWriter
{
    public static string Generate (ICommandRegistry registry, CliHostOptions options)
    {
        StringBuilder sb = new ();
        sb.Append ("{\"opencli\":\"0.1\",\"info\":{\"title\":");
        AppendJsonString (sb, options.ApplicationName);
        sb.Append (",\"version\":");
        AppendJsonString (sb, options.Version ?? "0.0.0");
        sb.Append ("},\"command\":{\"name\":");
        AppendJsonString (sb, options.ApplicationName);
        sb.Append (",\"commands\":[");

        bool first = true;

        foreach (ICliCommand cmd in registry.All)
        {
            if (!first)
            {
                sb.Append (',');
            }

            first = false;
            sb.Append ("{\"name\":");
            AppendJsonString (sb, cmd.PrimaryAlias);
            sb.Append (",\"aliases\":[");
            bool firstAlias = true;

            foreach (string alias in cmd.Aliases)
            {
                if (!firstAlias)
                {
                    sb.Append (',');
                }

                firstAlias = false;
                AppendJsonString (sb, alias);
            }

            sb.Append ("],\"description\":");
            AppendJsonString (sb, cmd.Description);
            sb.Append (",\"interactive\":true");
            sb.Append (",\"options\":[");
            bool firstOpt = true;

            foreach (CommandOptionDescriptor opt in cmd.Options)
            {
                if (!firstOpt)
                {
                    sb.Append (',');
                }

                firstOpt = false;
                sb.Append ("{\"name\":\"--");
                sb.Append (opt.Name);
                sb.Append ('"');

                if (opt.ShortName is not null)
                {
                    sb.Append (",\"aliases\":[\"-");
                    sb.Append (opt.ShortName);
                    sb.Append ("\"]");
                }

                sb.Append (",\"description\":");
                AppendJsonString (sb, opt.Description);
                sb.Append ('}');
            }

            sb.Append ("],\"exitCodes\":[");
            sb.Append ("{\"code\":0,\"description\":\"Success\"}");
            sb.Append (",{\"code\":2,\"description\":\"Usage error\"}");
            sb.Append (",{\"code\":130,\"description\":\"Cancelled\"}");
            sb.Append (']');
            sb.Append (",\"metadata\":[");
            sb.Append ("{\"name\":\"kind\",\"value\":");
            AppendJsonString (sb, cmd.Kind == CommandKind.Input ? "input" : "viewer");
            sb.Append ("},{\"name\":\"resultType\",\"value\":");
            AppendJsonString (sb, TypeNames.WireName (cmd.ResultType));
            sb.Append ('}');
            sb.Append ("]}");
        }

        sb.Append ("],\"options\":[");
        sb.Append ("{\"name\":\"--json\",\"aliases\":[\"-j\"],\"description\":\"Output JSON envelope\",\"recursive\":true}");
        sb.Append (",{\"name\":\"--initial\",\"aliases\":[\"-i\"],\"description\":\"Pre-fill value\",\"recursive\":true,\"arguments\":[{\"name\":\"value\",\"required\":true}]}");
        sb.Append (",{\"name\":\"--timeout\",\"description\":\"Cancel after duration\",\"recursive\":true,\"arguments\":[{\"name\":\"duration\",\"required\":true}]}");
        sb.Append ("],\"exitCodes\":[");
        sb.Append ("{\"code\":0,\"description\":\"Success\"}");
        sb.Append (",{\"code\":2,\"description\":\"Usage error\"}");
        sb.Append (",{\"code\":130,\"description\":\"Cancelled\"}");
        sb.Append ("]}}");

        return sb.ToString ();
    }

    private static void AppendJsonString (StringBuilder sb, string value)
    {
        sb.Append ('"');

        foreach (char c in value)
        {
            switch (c)
            {
                case '"': sb.Append ("\\\""); break;
                case '\\': sb.Append ("\\\\"); break;
                case '\b': sb.Append ("\\b"); break;
                case '\f': sb.Append ("\\f"); break;
                case '\n': sb.Append ("\\n"); break;
                case '\r': sb.Append ("\\r"); break;
                case '\t': sb.Append ("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append ("\\u").Append (((int)c).ToString ("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append (c);
                    }

                    break;
            }
        }

        sb.Append ('"');
    }
}
