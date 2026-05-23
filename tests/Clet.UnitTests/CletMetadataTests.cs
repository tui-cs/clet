#pragma warning disable xUnit1026 // Theory methods sharing MemberData intentionally ignore unused columns

using Terminal.Gui.Cli;
using Xunit;

namespace Clet.UnitTests;

/// <summary>
/// Shared Theory-driven tests for common clet metadata properties (PrimaryAlias, Kind, ResultType,
/// Description, Aliases, AcceptsPositionalArgs). Reduces ~50 lines of boilerplate per clet to a
/// single data row.
/// </summary>
public class CletMetadataTests
{
    private static ICommandRegistry SharedRegistry ()
    {
        ICommandRegistry registry = new CommandRegistry ();
        BuiltInClets.RegisterAll (registry);

        return registry;
    }

    public static IEnumerable<object[]> AllCletMetadata ()
    {
        ICommandRegistry registry = SharedRegistry ();

        yield return [new SelectClet (), "select", CommandKind.Input, typeof (string), true];
        yield return [new IntClet (), "int", CommandKind.Input, typeof (int), false];
        yield return [new DecimalClet (), "decimal", CommandKind.Input, typeof (decimal), false];
        yield return [new TextClet (), "text", CommandKind.Input, typeof (string), false];
        yield return [new ConfirmClet (), "confirm", CommandKind.Input, typeof (bool), false];
        yield return [new ColorClet (), "color", CommandKind.Input, typeof (string), false];
        yield return [new DateClet (), "date", CommandKind.Input, typeof (string), false];
        yield return [new TimeClet (), "time", CommandKind.Input, typeof (string), false];
        yield return [new DurationClet (), "duration", CommandKind.Input, typeof (string), false];
        yield return [new MultiSelectClet (), "multi-select", CommandKind.Input, typeof (System.Text.Json.Nodes.JsonArray), true];
        yield return [new LinearRangeClet (), "linear-range", CommandKind.Input, typeof (System.Text.Json.Nodes.JsonObject), true];
        yield return [new PickFileClet (), "pick-file", CommandKind.Input, typeof (System.Text.Json.Nodes.JsonNode), false];
        yield return [new PickDirectoryClet (), "pick-directory", CommandKind.Input, typeof (string), false];
        yield return [new MarkdownClet (), "md", CommandKind.Viewer, typeof (void), true];
        yield return [new HelpClet (registry), "help", CommandKind.Viewer, typeof (void), true];
    }

    [Theory]
    [MemberData (nameof (AllCletMetadata))]
    internal void PrimaryAlias_MatchesExpected (ICliCommand clet, string expectedAlias, CommandKind _, Type __, bool ___)
    {
        Assert.Equal (expectedAlias, clet.PrimaryAlias);
    }

    [Theory]
    [MemberData (nameof (AllCletMetadata))]
    internal void Kind_MatchesExpected (ICliCommand clet, string _, CommandKind expectedKind, Type __, bool ___)
    {
        Assert.Equal (expectedKind, clet.Kind);
    }

    [Theory]
    [MemberData (nameof (AllCletMetadata))]
    internal void ResultType_MatchesExpected (ICliCommand clet, string _, CommandKind __, Type expectedType, bool ___)
    {
        Assert.Equal (expectedType, clet.ResultType);
    }

    [Theory]
    [MemberData (nameof (AllCletMetadata))]
    internal void Description_IsNotEmpty (ICliCommand clet, string _, CommandKind __, Type ___, bool ____)
    {
        Assert.NotEmpty (clet.Description);
    }

    [Theory]
    [MemberData (nameof (AllCletMetadata))]
    internal void Aliases_ContainsPrimaryAlias (ICliCommand clet, string expectedAlias, CommandKind _, Type __, bool ___)
    {
        Assert.Contains (expectedAlias, clet.Aliases);
    }

    [Theory]
    [MemberData (nameof (AllCletMetadata))]
    internal void AcceptsPositionalArgs_MatchesExpected (ICliCommand clet, string _, CommandKind __, Type ___, bool expectedPositional)
    {
        Assert.Equal (expectedPositional, clet.AcceptsPositionalArgs);
    }
}
