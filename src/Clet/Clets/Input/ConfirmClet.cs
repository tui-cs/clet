using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class ConfirmClet : ICliCommand<bool?>
{
    public string PrimaryAlias => "confirm";
    public IReadOnlyList<string> Aliases => ["confirm"];
    public string Description => "Prompts for a yes/no confirmation and returns a boolean.";
    public CommandKind Kind => CommandKind.Input;
    public Type ResultType => typeof (bool);

    public IReadOnlyList<CommandOptionDescriptor> Options => [];

    public bool TryValidateInitial (string initial, CommandRunOptions options)
        => string.Equals (initial, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals (initial, "yes", StringComparison.OrdinalIgnoreCase)
           || string.Equals (initial, "false", StringComparison.OrdinalIgnoreCase)
           || string.Equals (initial, "no", StringComparison.OrdinalIgnoreCase);

    public async Task<CommandResult<bool?>> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken)
    {
        OptionSelector selector = new ()
        {
            Labels = [Terminal.Gui.Resources.Strings.btnYes, Terminal.Gui.Resources.Strings.btnNo],
            AssignHotKeys = true,
        };

        if (initial is not null)
        {
            if (string.Equals (initial, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals (initial, "yes", StringComparison.OrdinalIgnoreCase))
            {
                selector.Value = 0;
            }
            else if (string.Equals (initial, "false", StringComparison.OrdinalIgnoreCase)
                     || string.Equals (initial, "no", StringComparison.OrdinalIgnoreCase))
            {
                selector.Value = 1;
            }
        }

        string defaultTitle = "Confirm (Enter to accept, Esc to cancel)";

        RunnableWrapper<OptionSelector, int?> wrapper = new (selector);

        return await InputCletRunner.RunAsync<OptionSelector, int?, bool?> (
            app, wrapper, options,
            defaultTitle,
            cancellationToken,
            result =>
            {
                bool? value = result switch
                {
                    0 => true,
                    1 => false,
                    _ => null,
                };

                return new (CommandStatus.Ok, value, null, null);
            },
            addEnterBinding: false);
    }
}
