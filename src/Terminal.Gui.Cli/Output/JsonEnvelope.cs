using System.Text.Json;

namespace Terminal.Gui.Cli;

/// <summary>The stable wire format for CLI output.</summary>
public sealed class JsonEnvelope
{
    public int SchemaVersion { get; init; } = 1;
    public string Status { get; init; } = "ok";
    public object? Value { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }

    public static JsonEnvelope Ok (object? value = null) => new () { Status = "ok", Value = value };
    public static JsonEnvelope Cancelled () => new () { Status = "cancelled" };
    public static JsonEnvelope Error (string code, string message) =>
        new () { Status = "error", Code = code, Message = message };
    public static JsonEnvelope NoResult () => new () { Status = "no-result" };

    /// <summary>Serialize using source-generated context (AOT-safe).</summary>
    public string ToJson ()
    {
        return JsonSerializer.Serialize (this, CliJsonContext.Default.JsonEnvelope);
    }
}
