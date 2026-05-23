using Xunit;

namespace Terminal.Gui.Cli.Tests;

public class ExitCodesTests
{
    [Fact]
    public void FromResult_Ok_Returns0 ()
    {
        CommandResult result = new (CommandStatus.Ok, "value", null, null);

        Assert.Equal (0, ExitCodes.FromResult (result));
    }

    [Fact]
    public void FromResult_Cancelled_Returns130 ()
    {
        CommandResult result = new (CommandStatus.Cancelled, null, null, null);

        Assert.Equal (130, ExitCodes.FromResult (result));
    }

    [Fact]
    public void FromResult_NoResult_Returns1 ()
    {
        CommandResult result = new (CommandStatus.NoResult, null, null, null);

        Assert.Equal (1, ExitCodes.FromResult (result));
    }

    [Fact]
    public void FromResult_Error_Validation_Returns65 ()
    {
        CommandResult result = new (CommandStatus.Error, null, "validation", "bad input");

        Assert.Equal (65, ExitCodes.FromResult (result));
    }

    [Fact]
    public void FromResult_Error_InputTooLarge_Returns65 ()
    {
        CommandResult result = new (CommandStatus.Error, null, "input-too-large", "too big");

        Assert.Equal (65, ExitCodes.FromResult (result));
    }

    [Fact]
    public void FromResult_Error_Io_Returns74 ()
    {
        CommandResult result = new (CommandStatus.Error, null, "io", "disk full");

        Assert.Equal (74, ExitCodes.FromResult (result));
    }

    [Fact]
    public void FromResult_Error_Unknown_Returns2 ()
    {
        CommandResult result = new (CommandStatus.Error, null, "something-else", "oops");

        Assert.Equal (2, ExitCodes.FromResult (result));
    }
}
