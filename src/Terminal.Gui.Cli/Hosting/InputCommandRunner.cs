using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Terminal.Gui.Cli;

/// <summary>
/// Shared boilerplate for input commands that wrap a control in <see cref="RunnableWrapper{TView, TResult}"/>.
/// Handles: cancellation pre-check → wrapper styling → RunAsync → catch → post-cancel check → result extraction.
/// </summary>
public static class InputCommandRunner
{
    /// <summary>
    /// Configures, runs, and extracts the result from an input command wrapper.
    /// </summary>
    public static async Task<CommandResult<TValue>> RunAsync<TControl, TRawResult, TValue> (
        IApplication app,
        RunnableWrapper<TControl, TRawResult> wrapper,
        CommandRunOptions options,
        string defaultTitle,
        CancellationToken cancellationToken,
        Func<TRawResult?, CommandResult<TValue>> resultMapper,
        bool addEnterBinding = true)
        where TControl : View, new()
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new (CommandStatus.Cancelled, default, null, null);
        }

        // Apply standard styling
        wrapper.Title = options.Title ?? defaultTitle;
        wrapper.Width = Dim.Fill ();
        wrapper.BorderStyle = LineStyle.Rounded;
        wrapper.Border.Thickness = new Thickness (0, 1, 0, 0);

        if (addEnterBinding)
        {
            wrapper.KeyBindings.Add (Key.Enter, Command.Accept);
        }

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

        return resultMapper (wrapper.Result);
    }

    /// <summary>
    /// Simplified overload that returns the wrapper result directly as the value.
    /// </summary>
    public static Task<CommandResult<TValue>> RunAsync<TControl, TValue> (
        IApplication app,
        RunnableWrapper<TControl, TValue> wrapper,
        CommandRunOptions options,
        string defaultTitle,
        CancellationToken cancellationToken,
        bool addEnterBinding = true)
        where TControl : View, new()
    {
        return RunAsync<TControl, TValue, TValue> (
            app, wrapper, options, defaultTitle, cancellationToken,
            result => new (CommandStatus.Ok, result, null, null),
            addEnterBinding);
    }
}
