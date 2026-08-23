using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Terminal.Gui;
using Terminal.Gui.App;
using Trace = Terminal.Gui.Tracing.Trace;

namespace Clet;

/// <summary>
/// Configures Terminal.Gui's <see cref="Logging"/> and <see cref="Trace"/> systems for clet.
/// In DEBUG builds, logs to <c>logs/clet.log</c> and enables Configuration tracing.
/// In Release builds, this is a no-op (all methods are <c>[Conditional("DEBUG")]</c>).
/// </summary>
internal static class CletLogging
{
    private const string LogDirectory = "logs";
    private const string LogFileName = "clet.log";

    /// <summary>
    /// Initializes file-based logging and TG tracing. Call once at startup, before
    /// <see cref="CletConfiguration.Apply"/>.
    /// </summary>
    [Conditional ("DEBUG")]
    internal static void Initialize ()
    {
        Logging.Logger = CreateFileLogger ();
        Logging.Information ("clet logging initialized");

        // Enable Configuration tracing so Load/Apply issues are captured in the log file.
        Trace.EnabledCategories = TraceCategory.Configuration | TraceCategory.Lifecycle;
        Logging.Information ($"Trace categories enabled: {Trace.EnabledCategories}");
    }

    /// <summary>
    /// Creates a simple file-based <see cref="ILogger"/> that writes to <c>logs/clet.log</c>
    /// relative to the binary directory. No external dependencies required.
    /// </summary>
    private static ILogger CreateFileLogger ()
    {
        string logDir = Path.Combine (AppContext.BaseDirectory, LogDirectory);
        Directory.CreateDirectory (logDir);
        string logPath = Path.Combine (logDir, LogFileName);

        return new FileLogger (logPath);
    }

    /// <summary>Writes log entries to a file in a thread-safe manner. No external dependencies.</summary>
    private sealed class FileLogger : ILogger
    {
        private readonly StreamWriter _writer;
        private readonly Lock _lock = new ();

        internal FileLogger (string path)
        {
            _writer = new StreamWriter (path, append: true) { AutoFlush = true };
        }

        public IDisposable? BeginScope<TState> (TState state) where TState : notnull => null;

        public bool IsEnabled (LogLevel logLevel) => logLevel >= LogLevel.Trace;

        public void Log<TState> (
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled (logLevel))
            {
                return;
            }

            string timestamp = DateTime.Now.ToString ("yyyy-MM-dd HH:mm:ss.fff");
            string level = logLevel switch
            {
                LogLevel.Trace => "TRC",
                LogLevel.Debug => "DBG",
                LogLevel.Information => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                LogLevel.Critical => "CRT",
                _ => "???"
            };

            string message = formatter (state, exception);

            lock (_lock)
            {
                _writer.WriteLine ($"{timestamp} [{level}] {message}");

                if (exception is not null)
                {
                    _writer.WriteLine ($"  Exception: {exception}");
                }
            }
        }
    }
}
