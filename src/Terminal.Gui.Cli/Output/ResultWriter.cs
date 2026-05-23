using System.Text.Json.Nodes;

namespace Terminal.Gui.Cli;

/// <summary>Formats command results to stdout (plain text or JSON envelope).</summary>
public static class ResultWriter
{
    public static bool Write (CommandResult result, bool jsonOutput, TextWriter stdout, TextWriter stderr, string? outputPath = null)
    {
        TextWriter target;

        if (outputPath is not null)
        {
            if (result.Status != CommandStatus.Ok)
            {
                WriteCore (result, jsonOutput, stdout, stderr);

                return true;
            }

            try
            {
                FileStream stream = new (outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                target = new StreamWriter (stream, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                stderr.WriteLine ($"error: cannot write to '{outputPath}': {ex.Message}");

                return false;
            }
        }
        else
        {
            target = stdout;
        }

        try
        {
            WriteCore (result, jsonOutput, target, stderr);
        }
        finally
        {
            if (outputPath is not null)
            {
                target.Dispose ();
            }
        }

        return true;
    }

    private static void WriteCore (CommandResult result, bool jsonOutput, TextWriter target, TextWriter stderr)
    {
        if (jsonOutput)
        {
            target.WriteLine (ToEnvelope (result).ToJson ());

            return;
        }

        switch (result.Status)
        {
            case CommandStatus.Ok:
                if (result.Value is not null)
                {
                    switch (result.Value)
                    {
                        case JsonArray arr:
                            foreach (JsonNode? item in arr)
                            {
                                target.WriteLine (item?.ToString ());
                            }

                            break;
                        case JsonNode node:
                            target.WriteLine (node.ToJsonString ());

                            break;
                        default:
                            target.WriteLine (result.Value);

                            break;
                    }
                }

                break;
            case CommandStatus.Cancelled:
                break;
            case CommandStatus.NoResult:
                break;
            case CommandStatus.Error:
                stderr.WriteLine ($"error: {result.ErrorCode}: {result.ErrorMessage}");

                break;
        }
    }

    private static JsonEnvelope ToEnvelope (CommandResult result)
    {
        return result.Status switch
        {
            CommandStatus.Ok => JsonEnvelope.Ok (result.Value),
            CommandStatus.Cancelled => JsonEnvelope.Cancelled (),
            CommandStatus.NoResult => JsonEnvelope.NoResult (),
            CommandStatus.Error => JsonEnvelope.Error (
                result.ErrorCode ?? "unknown",
                result.ErrorMessage ?? string.Empty),
            _ => JsonEnvelope.Error ("unknown", $"unexpected status {result.Status}"),
        };
    }
}
