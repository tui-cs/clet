using Terminal.Gui.App;
using Terminal.Gui.Cli;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

/// <summary>
/// Shared boilerplate for input clets that wrap a control in <see cref="RunnableWrapper{TView, TResult}"/>.
/// Handles: cancellation pre-check → wrapper styling → RunAsync → catch → post-cancel check → result extraction.
/// Adds clet-specific styling (<see cref="CletStyling.BaseSchemeName"/>) on top of the library's runner.
/// </summary>
internal static class InputCletRunner
{
    /// <summary>
    /// Configures, runs, and extracts the result from an input clet wrapper.
    /// </summary>
    /// <typeparam name="TControl">The TG View type.</typeparam>
    /// <typeparam name="TRawResult">The raw result type from the wrapper.</typeparam>
    /// <typeparam name="TValue">The final value type returned in the <see cref="CommandResult{T}"/>.</typeparam>
    /// <param name="app">The TG application instance.</param>
    /// <param name="wrapper">The pre-configured wrapper (control + optional ResultExtractor already set).</param>
    /// <param name="options">Command run options (used for Title).</param>
    /// <param name="defaultTitle">Default title when <c>options.Title</c> is null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="resultMapper">Maps the wrapper's raw result to the command's return value.</param>
    /// <param name="addEnterBinding">Whether to add Key.Enter → Command.Accept binding. Default true.</param>
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
        wrapper.SchemeName = CletStyling.BaseSchemeName;
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
