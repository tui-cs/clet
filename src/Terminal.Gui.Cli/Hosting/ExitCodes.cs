namespace Terminal.Gui.Cli;

/// <summary>POSIX-conventional exit codes.</summary>
public static class ExitCodes
{
    public const int Ok = 0;
    public const int NoResult = 1;
    public const int UsageError = 2;
    public const int ValidationError = 65;
    public const int IoError = 74;
    public const int Cancelled = 130;

    public static int FromResult (CommandResult result)
    {
        return result.Status switch
        {
            CommandStatus.Ok => Ok,
            CommandStatus.Cancelled => Cancelled,
            CommandStatus.NoResult => NoResult,
            CommandStatus.Error => result.ErrorCode switch
            {
                "validation" => ValidationError,
                "input-too-large" => ValidationError,
                "io" => IoError,
                _ => UsageError,
            },
            _ => UsageError,
        };
    }
}
