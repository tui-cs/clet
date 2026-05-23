using System.Globalization;
using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class DateClet : ICliCommand<string?>
{
    public string PrimaryAlias => "date";
    public IReadOnlyList<string> Aliases => ["date"];
    public string Description => "Prompts for a date and returns an ISO-8601 date string (YYYY-MM-DD).";
    public CommandKind Kind => CommandKind.Input;
    public Type ResultType => typeof (string);

    public IReadOnlyList<CommandOptionDescriptor> Options => [];

    public bool TryValidateInitial (string initial, CommandRunOptions options)
        => DateTime.TryParse (initial, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public async Task<CommandResult<string?>> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken)
    {
        DatePicker picker = new ();

        if (initial is not null
            && DateTime.TryParse (initial, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime initialDate))
        {
            picker.Value = initialDate;
        }

        RunnableWrapper<DatePicker, DateTime?> wrapper = new (picker)
        {
            ResultExtractor = p => p.Value,
        };

        return await InputCletRunner.RunAsync<DatePicker, DateTime?, string?> (
            app, wrapper, options,
            "Select a date (Enter to accept, Esc to cancel)",
            cancellationToken,
            result =>
            {
                string? formatted = result?.ToString ("yyyy-MM-dd", CultureInfo.InvariantCulture);

                return new (CommandStatus.Ok, formatted, null, null);
            });
    }
}
