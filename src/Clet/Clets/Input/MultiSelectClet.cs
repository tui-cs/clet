using System.Text.Json.Nodes;
using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class MultiSelectClet : ICliCommand<JsonArray?>
{
    public string PrimaryAlias => "multi-select";
    public IReadOnlyList<string> Aliases => ["multi-select"];
    public string Description => "Presents a list of options with checkboxes and returns the selected texts.";
    public CommandKind Kind => CommandKind.Input;
    public Type ResultType => typeof (JsonArray);

    public IReadOnlyList<CommandOptionDescriptor> Options =>
    [
        new("options", "o", typeof(string), "Comma-separated list of options to display.", true, null),
    ];

    public bool AcceptsPositionalArgs => true;

    public async Task<CommandResult<JsonArray?>> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new (CommandStatus.Cancelled, default, null, null);
        }

        string[] labels = options.Arguments is { Count: > 0 }
            ? LabelParser.Split (options.Arguments)
            : options.CommandOptions.TryGetValue ("options", out string? optionsValue)
                ? LabelParser.Split (optionsValue)
                : [];

        int[] values = new int[labels.Length];

        for (int i = 0; i < labels.Length; i++)
        {
            values[i] = 1 << i;
        }

        FlagSelector flagSelector = new ()
        {
            Labels = labels,
            Values = values,
        };

        if (initial is not null)
        {
            string[] initialLabels = initial.Split (',');
            int flags = 0;

            for (int i = 0; i < labels.Length; i++)
            {
                if (Array.Exists (initialLabels,
                        l => string.Equals (l.Trim (), labels[i], StringComparison.OrdinalIgnoreCase)))
                {
                    flags |= 1 << i;
                }
            }

            flagSelector.Value = flags;
        }

        RunnableWrapper<FlagSelector, int?> wrapper = new (flagSelector)
        {
            Title = options.Title ?? "Select one or more options (Enter to accept, Esc to cancel)",
            Width = Dim.Fill (),
            BorderStyle = LineStyle.Rounded,
            SchemeName = CletStyling.BaseSchemeName,
        };
        wrapper.Border.Thickness = new Thickness (0, 1, 0, 0);

        try
        {
            await app.RunAsync (wrapper, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new (CommandStatus.Cancelled, default, null, null);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new (CommandStatus.Cancelled, default, null, null);
        }

        int? resultFlags = wrapper.Result;
        JsonArray selected = [];

        if (resultFlags is { } bits)
        {
            for (int i = 0; i < labels.Length; i++)
            {
                if ((bits & (1 << i)) != 0)
                {
                    selected.Add ((JsonNode)labels[i]);
                }
            }
        }

        return new (CommandStatus.Ok, selected, null, null);
    }
}
