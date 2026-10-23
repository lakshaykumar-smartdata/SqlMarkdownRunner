using System.Collections.Concurrent;

namespace SqlMarkdownRunner;

/// <summary>
/// Warnings and errors appended to logs/app-yyyy-MM-dd.log. The app runs from a cmd window that
/// gets closed, so console output answers nothing the next morning — a file does.
/// </summary>
// ponytail: one file per day, appended under a lock, no rotation or retention. Only faults are
// written so it stays small. Swap in Serilog if it ever needs structure, sinks or shipping.
public sealed class FileLoggerProvider(string folder, LogLevel minimum = LogLevel.Warning) : ILoggerProvider
{
    private static readonly object Gate = new();

    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    internal LogLevel Minimum => minimum;

    public ILogger CreateLogger(string category) => _loggers.GetOrAdd(category, c => new FileLogger(this, c));

    public void Dispose() => _loggers.Clear();

    internal void Append(string line)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(folder);
                File.AppendAllText(
                    Path.Combine(folder, $"app-{DateTime.Now:yyyy-MM-dd}.log"),
                    line + Environment.NewLine);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging is never allowed to be the thing that breaks the app.
        }
    }
}

internal sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel level) => level != LogLevel.None && level >= provider.Minimum;

    public void Log<TState>(
        LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(level)) return;

        // The account is recorded now so the log still identifies people if this ever moves to IIS.
        var line = $"{DateTime.Now:HH:mm:ss} {level,-11} {AppUser.Name} {Short(category)}: {formatter(state, error)}";
        if (error is not null) line += Environment.NewLine + error;

        provider.Append(line);
    }

    private static string Short(string category) => category[(category.LastIndexOf('.') + 1)..];
}
