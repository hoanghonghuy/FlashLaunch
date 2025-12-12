using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace FlashLaunch.Core.Logging;

public sealed class FileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly string _filePath;
    private readonly LogLevel _minimumLevel;
    private readonly Func<string?, LogLevel, bool>? _filter;
    private IExternalScopeProvider? _scopeProvider;
    private readonly object _sync = new();
    private StreamWriter? _writer;

    public FileLoggerProvider(string filePath, LogLevel minimumLevel, Func<string?, LogLevel, bool>? filter = null)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _minimumLevel = minimumLevel;
        _filter = filter;
        InitializeWriter();
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, CreateLoggerCore);

    private FileLogger CreateLoggerCore(string categoryName) =>
        new(categoryName, ShouldLog, WriteMessage, () => _scopeProvider);

    public void Dispose()
    {
        lock (_sync)
        {
            _writer?.Dispose();
            _writer = null;
        }

        _loggers.Clear();
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    private bool ShouldLog(string? category, LogLevel level)
    {
        if (level < _minimumLevel)
        {
            return false;
        }

        if (_filter is null)
        {
            return true;
        }

        return _filter(category, level);
    }

    private void WriteMessage(LogEntry entry, Exception? exception, IExternalScopeProvider? scopeProvider)
    {
        var builder = new StringBuilder();
        builder.Append(DateTimeOffset.Now.ToString("u"));
        builder.Append(" [").Append(entry.Level).Append("] ");
        builder.Append(entry.Category).Append(": ");
        builder.Append(entry.Message);

        if (exception is not null)
        {
            builder.Append(" | ").Append(exception);
        }

        scopeProvider?.ForEachScope((scope, state) =>
        {
            state.Append(" | Scope: ").Append(scope);
        }, builder);

        var line = builder.ToString();

        lock (_sync)
        {
            _writer?.WriteLine(line);
            _writer?.Flush();
        }
    }

    private void InitializeWriter()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        lock (_sync)
        {
            _writer = new StreamWriter(new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true
            };
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly Func<string?, LogLevel, bool> _shouldLog;
        private readonly Action<LogEntry, Exception?, IExternalScopeProvider?> _write;
        private readonly Func<IExternalScopeProvider?> _scopeAccessor;

        public FileLogger(
            string category,
            Func<string?, LogLevel, bool> shouldLog,
            Action<LogEntry, Exception?, IExternalScopeProvider?> write,
            Func<IExternalScopeProvider?> scopeAccessor)
        {
            _category = category;
            _shouldLog = shouldLog;
            _write = write;
            _scopeAccessor = scopeAccessor;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => _shouldLog(_category, logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var entry = new LogEntry(_category, logLevel, message, eventId);
            _write(entry, exception, _scopeAccessor());
        }
    }

    private readonly struct LogEntry
    {
        public LogEntry(string category, LogLevel level, string message, EventId eventId)
        {
            Category = category;
            Level = level;
            Message = message;
            EventId = eventId;
        }

        public string Category { get; }
        public LogLevel Level { get; }
        public string Message { get; }
        public EventId EventId { get; }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
