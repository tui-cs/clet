using System.Text.Json;
using Terminal.Gui.Cli;

namespace Clet;

internal static class CletExitCodes
{
    public static int FromResult (CommandResult result)
    {
        if (result.Status == CommandStatus.Error && result.ErrorCode == "input-too-large")
        {
            return ExitCodes.ValidationError;
        }

        return ExitCodes.FromResult (result);
    }

    public static int MapPackageExit (int exitCode, string stdout)
    {
        if (exitCode == ExitCodes.UsageError && HasInputTooLargeCode (stdout))
        {
            return ExitCodes.ValidationError;
        }

        return exitCode;
    }

    private static bool HasInputTooLargeCode (string stdout)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse (stdout);

            return document.RootElement.TryGetProperty ("code", out JsonElement code)
                   && code.ValueKind == JsonValueKind.String
                   && code.GetString () == "input-too-large";
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
