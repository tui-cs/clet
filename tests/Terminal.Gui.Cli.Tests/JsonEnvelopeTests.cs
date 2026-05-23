using System.Text.Json;
using Xunit;

namespace Terminal.Gui.Cli.Tests;

public class JsonEnvelopeTests
{
    [Fact]
    public void Ok_ProducesCorrectJson ()
    {
        JsonEnvelope envelope = JsonEnvelope.Ok ("hello");
        string json = envelope.ToJson ();

        using JsonDocument doc = JsonDocument.Parse (json);
        JsonElement root = doc.RootElement;

        Assert.Equal (1, root.GetProperty ("schemaVersion").GetInt32 ());
        Assert.Equal ("ok", root.GetProperty ("status").GetString ());
        Assert.Equal ("hello", root.GetProperty ("value").GetString ());
        Assert.False (root.TryGetProperty ("code", out _));
        Assert.False (root.TryGetProperty ("message", out _));
    }

    [Fact]
    public void Cancelled_OmitsValueAndCode ()
    {
        JsonEnvelope envelope = JsonEnvelope.Cancelled ();
        string json = envelope.ToJson ();

        using JsonDocument doc = JsonDocument.Parse (json);
        JsonElement root = doc.RootElement;

        Assert.Equal ("cancelled", root.GetProperty ("status").GetString ());
        Assert.False (root.TryGetProperty ("value", out _));
        Assert.False (root.TryGetProperty ("code", out _));
    }

    [Fact]
    public void Error_IncludesCodeAndMessage ()
    {
        JsonEnvelope envelope = JsonEnvelope.Error ("io", "disk full");
        string json = envelope.ToJson ();

        using JsonDocument doc = JsonDocument.Parse (json);
        JsonElement root = doc.RootElement;

        Assert.Equal ("error", root.GetProperty ("status").GetString ());
        Assert.Equal ("io", root.GetProperty ("code").GetString ());
        Assert.Equal ("disk full", root.GetProperty ("message").GetString ());
    }

    [Fact]
    public void NoResult_HasCorrectStatus ()
    {
        JsonEnvelope envelope = JsonEnvelope.NoResult ();
        string json = envelope.ToJson ();

        using JsonDocument doc = JsonDocument.Parse (json);
        JsonElement root = doc.RootElement;

        Assert.Equal ("no-result", root.GetProperty ("status").GetString ());
        Assert.False (root.TryGetProperty ("value", out _));
    }
}
