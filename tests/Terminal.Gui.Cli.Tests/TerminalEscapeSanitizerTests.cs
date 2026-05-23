using Xunit;

namespace Terminal.Gui.Cli.Tests;

public class TerminalEscapeSanitizerTests
{
    [Fact]
    public void Sanitize_Null_ReturnsNull ()
    {
        Assert.Null (TerminalEscapeSanitizer.Sanitize (null));
    }

    [Fact]
    public void Sanitize_Empty_ReturnsEmpty ()
    {
        Assert.Equal (string.Empty, TerminalEscapeSanitizer.Sanitize (string.Empty));
    }

    [Fact]
    public void Sanitize_CleanInput_Unchanged ()
    {
        Assert.Equal ("hello world", TerminalEscapeSanitizer.Sanitize ("hello world"));
    }

    [Fact]
    public void Sanitize_StripsBareEsc ()
    {
        Assert.Equal ("ab", TerminalEscapeSanitizer.Sanitize ("a\u001b" + "b"));
    }

    [Fact]
    public void Sanitize_StripsBel ()
    {
        Assert.Equal ("ab", TerminalEscapeSanitizer.Sanitize ("a\u0007" + "b"));
    }

    [Fact]
    public void Sanitize_Strips8BitCsi ()
    {
        Assert.Equal ("ab", TerminalEscapeSanitizer.Sanitize ("a\u009b" + "b"));
    }

    [Fact]
    public void Sanitize_Strips8BitOsc ()
    {
        Assert.Equal ("ab", TerminalEscapeSanitizer.Sanitize ("a\u009d" + "b"));
    }

    [Fact]
    public void Sanitize_StripsC1SevenBitPair ()
    {
        // ESC followed by @ through _ is a C1 pair — both bytes stripped
        Assert.Equal ("ab", TerminalEscapeSanitizer.Sanitize ("a\u001b@b"));
        Assert.Equal ("ab", TerminalEscapeSanitizer.Sanitize ("a\u001b_b"));
    }

    [Fact]
    public void SanitizeRenderedOutput_PreservesSgr ()
    {
        string input = "\u001b[31mred\u001b[0m";

        Assert.Equal (input, TerminalEscapeSanitizer.SanitizeRenderedOutput (input));
    }

    [Fact]
    public void SanitizeRenderedOutput_StripsNonHyperlinkOsc ()
    {
        // OSC 0 (set title) — should be stripped
        string input = "before\u001b]0;evil title\u0007after";
        string result = TerminalEscapeSanitizer.SanitizeRenderedOutput (input);

        Assert.Equal ("beforeafter", result);
    }

    [Fact]
    public void SanitizeRenderedOutput_PreservesOsc8Hyperlink ()
    {
        // OSC 8 hyperlink — should be preserved
        string input = "\u001b]8;;https://example.com\u0007link\u001b]8;;\u0007";
        string result = TerminalEscapeSanitizer.SanitizeRenderedOutput (input);

        Assert.Equal (input, result);
    }
}
