using System.Text.Json.Nodes;

namespace Terminal.Gui.Cli;

/// <summary>Maps CLR types to stable wire-format names (string, int, bool, etc.).</summary>
public static class TypeNames
{
    public static string WireName (Type type)
    {
        Type underlying = Nullable.GetUnderlyingType (type) ?? type;

        if (underlying == typeof (string))
        {
            return "string";
        }

        if (underlying == typeof (int) || underlying == typeof (long) || underlying == typeof (short))
        {
            return "int";
        }

        if (underlying == typeof (decimal) || underlying == typeof (double) || underlying == typeof (float))
        {
            return "decimal";
        }

        if (underlying == typeof (bool))
        {
            return "bool";
        }

        if (underlying == typeof (DateTime) || underlying == typeof (DateOnly))
        {
            return "date";
        }

        if (underlying == typeof (TimeOnly))
        {
            return "time";
        }

        if (underlying == typeof (TimeSpan))
        {
            return "duration";
        }

        if (underlying == typeof (JsonArray))
        {
            return "array";
        }

        if (underlying == typeof (JsonObject))
        {
            return "object";
        }

        if (underlying == typeof (JsonNode))
        {
            return "json";
        }

        if (underlying == typeof (void))
        {
            return "none";
        }

        return underlying.Name;
    }
}
