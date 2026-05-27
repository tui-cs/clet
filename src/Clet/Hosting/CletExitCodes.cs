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
}
