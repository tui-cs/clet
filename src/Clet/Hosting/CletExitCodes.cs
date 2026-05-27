using Terminal.Gui.Cli;

namespace Clet;

internal static class CletExitCodes
{
    private const string InputTooLargeJsonCode = "\"code\":\"input-too-large\"";

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
        if (exitCode == ExitCodes.UsageError && stdout.Contains (InputTooLargeJsonCode, StringComparison.Ordinal))
        {
            return ExitCodes.ValidationError;
        }

        return exitCode;
    }
}
