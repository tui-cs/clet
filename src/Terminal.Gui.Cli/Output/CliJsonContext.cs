using System.Text.Json;
using System.Text.Json.Serialization;

namespace Terminal.Gui.Cli;

[JsonSourceGenerationOptions (
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable (typeof (JsonEnvelope))]
internal partial class CliJsonContext : JsonSerializerContext
{
}
