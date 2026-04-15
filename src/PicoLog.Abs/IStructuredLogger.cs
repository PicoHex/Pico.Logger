namespace PicoLog.Abs;

/// <summary>
/// Strict structured-logging contract for callers that require property preservation.
/// </summary>
/// <remarks>
/// Implementations preserve supplied structured <c>properties</c> onto <see cref="LogEntry.Properties"/>
/// rather than discarding them through a plain <see cref="ILogger"/> fallback path.
/// </remarks>
public interface IStructuredLogger : ILogger
{
    /// <summary>
    /// Logs a message while preserving structured <paramref name="properties"/>.
    /// </summary>
    void LogStructured(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties = null,
        Exception? exception = null
    );

    /// <summary>
    /// Asynchronously logs a message while preserving structured <paramref name="properties"/>.
    /// </summary>
    /// <remarks>
    /// Completion indicates that the logger accepted the write or finished any configured
    /// backpressure handling at the logger boundary. It does not, by itself, guarantee that the
    /// entry has already been durably written by downstream sinks.
    /// </remarks>
    Task LogStructuredAsync(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties = null,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Typed structured-logging contract for callers and DI consumers that require property preservation.
/// </summary>
public interface IStructuredLogger<out TCategory> : ILogger<TCategory>, IStructuredLogger;
