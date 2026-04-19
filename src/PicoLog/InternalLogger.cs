namespace PicoLog;

internal sealed class InternalLogger(
    string categoryName,
    LoggerFactoryRuntime runtime,
    CategoryPipeline pipeline
) : ILogger
{
    private readonly LoggerFactoryRuntime _runtime =
        runtime ?? throw new ArgumentNullException(nameof(runtime));
    private readonly CategoryPipeline _pipeline =
        pipeline ?? throw new ArgumentNullException(nameof(pipeline));

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return !_runtime.IsAcceptingWrites ? LoggerScopeProvider.Empty : _runtime.BeginScope(state);
    }

    public void Log(LogLevel logLevel, string message, Exception? exception = null) =>
        Write(logLevel, message, properties: null, exception);

    public void Log(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    ) => Write(logLevel, message, properties, exception);

    public Task LogAsync(
        LogLevel logLevel,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => WriteAsync(logLevel, message, properties: null, exception, cancellationToken);

    public Task LogAsync(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken = default
    ) => WriteAsync(logLevel, message, properties, exception, cancellationToken);

    private void Write(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    )
    {
        if (!CanAcceptWrite(logLevel))
            return;

        var entry = CreateEntry(logLevel, message, exception, properties);
        _pipeline.Write(entry);
    }

    private Task WriteAsync(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken
    )
    {
        if (!CanAcceptWrite(logLevel))
            return Task.CompletedTask;

        var entry = CreateEntry(logLevel, message, exception, properties);

        return _pipeline.WriteAsync(entry, cancellationToken);
    }

    private bool CanAcceptWrite(LogLevel logLevel)
    {
        if (_runtime.IsAcceptingWrites)
            return _runtime.IsEnabled(logLevel);
        _runtime.RecordRejectedAfterShutdown();
        return false;
    }

    private LogEntry CreateEntry(
        LogLevel logLevel,
        string message,
        Exception? exception,
        IReadOnlyList<KeyValuePair<string, object?>>? properties
    ) =>
        new()
        {
            Timestamp = GetTimestamp(),
            Level = logLevel,
            Category = categoryName,
            Message = message,
            Exception = exception,
            Scopes = _runtime.CaptureScopes(),
            Properties = SnapshotProperties(properties)
        };

    private static IReadOnlyList<KeyValuePair<string, object?>>? SnapshotProperties(
        IReadOnlyList<KeyValuePair<string, object?>>? properties
    )
    {
        if (properties is not { Count: > 0 })
            return null;

        if (properties is KeyValuePair<string, object?>[] array)
        {
            return array.Length switch
            {
                1 => [array[0]],
                2 => [array[0], array[1]],
                3 => [array[0], array[1], array[2]],
                4 => [array[0], array[1], array[2], array[3]],
                _ => array.ToArray()
            };
        }

        return properties.Count switch
        {
            1 => [properties[0]],
            2 => [properties[0], properties[1]],
            3 => [properties[0], properties[1], properties[2]],
            4 => [properties[0], properties[1], properties[2], properties[3]],
            _ => CopyProperties(properties)
        };
    }

    private static KeyValuePair<string, object?>[] CopyProperties(
        IReadOnlyList<KeyValuePair<string, object?>> properties
    )
    {
        var snapshot = new KeyValuePair<string, object?>[properties.Count];

        for (var index = 0; index < properties.Count; index++)
            snapshot[index] = properties[index];

        return snapshot;
    }

    private static DateTimeOffset GetTimestamp() => TimeProvider.System.GetLocalNow();
}
