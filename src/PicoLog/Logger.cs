namespace PicoLog;

/// <summary>
/// Typed adapter for <see cref="ILogger{TCategory}"/> and <see cref="IStructuredLogger{TCategory}"/>.
/// </summary>
/// <remarks>
/// When backed by PicoLog's built-in <see cref="LoggerFactory"/>, structured properties are preserved.
/// When backed by a custom <see cref="ILoggerFactory"/> that returns only plain <see cref="ILogger"/>,
/// structured extension calls fall back to best-effort semantics and may discard properties.
/// </remarks>
public sealed class Logger<TCategory> : IStructuredLogger<TCategory>
{
    private readonly ILogger _innerLogger;

    public Logger(ILoggerFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _innerLogger = factory.CreateLogger(typeof(TCategory).FullName!);
    }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull => _innerLogger.BeginScope(state);

    public void Log(LogLevel logLevel, string message, Exception? exception = null) =>
        _innerLogger.Log(logLevel, message, exception);

    public Task LogAsync(
        LogLevel logLevel,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => _innerLogger.LogAsync(logLevel, message, exception, cancellationToken);

    public void LogStructured(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties = null,
        Exception? exception = null
    ) => _innerLogger.LogStructured(logLevel, message, properties, exception);

    public Task LogStructuredAsync(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties = null,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => _innerLogger.LogStructuredAsync(
        logLevel,
        message,
        properties,
        exception,
        cancellationToken
    );
}
