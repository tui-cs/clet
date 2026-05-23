using System.Text.Json.Nodes;
using Xunit;

namespace Terminal.Gui.Cli.Tests;

public class TypeNamesTests
{
    [Theory]
    [InlineData (typeof (string), "string")]
    [InlineData (typeof (int), "int")]
    [InlineData (typeof (long), "int")]
    [InlineData (typeof (short), "int")]
    [InlineData (typeof (decimal), "decimal")]
    [InlineData (typeof (double), "decimal")]
    [InlineData (typeof (float), "decimal")]
    [InlineData (typeof (bool), "bool")]
    [InlineData (typeof (DateTime), "date")]
    [InlineData (typeof (DateOnly), "date")]
    [InlineData (typeof (TimeOnly), "time")]
    [InlineData (typeof (TimeSpan), "duration")]
    [InlineData (typeof (JsonArray), "array")]
    [InlineData (typeof (JsonObject), "object")]
    [InlineData (typeof (JsonNode), "json")]
    [InlineData (typeof (void), "none")]
    public void WireName_MapsCorrectly (Type type, string expected)
    {
        Assert.Equal (expected, TypeNames.WireName (type));
    }

    [Theory]
    [InlineData (typeof (int?), "int")]
    [InlineData (typeof (bool?), "bool")]
    [InlineData (typeof (DateTime?), "date")]
    public void WireName_Nullable_UnwrapsCorrectly (Type type, string expected)
    {
        Assert.Equal (expected, TypeNames.WireName (type));
    }
}
