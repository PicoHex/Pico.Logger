namespace PicoLog;

internal sealed class InternalLogger : IStructuredLogger, IDisposable, IAsyncDisposable
{
    private enum LogWriteResult
    {
        Accepted,
        Dropped,
        RejectedAfterShutdown
    }

    private readonly string _categoryName;
    private readonly Channel<LogEntry> _channel;
    private readonly ChannelWriter<LogEntry> _writer;
    private readonly ChannelReader<LogEntry> _reader;
    private readonly Task _processingTask;
    private readonly ILogSink[] _sinksArray;
    private readonly LoggerFactory _factory;
    private readonly ILogSink? _fallbackSink;
    private readonly int _queueCapacity;
    private readonly LogQueueFullMode _queueFullMode;
    private readonly TimeSpan _syncWriteTimeout;
    private readonly int _queueDepthProviderId;
    private int _disposeState;
    private int _queuedEntries;
    private long _droppedEntries;

    public InternalLogger(string categoryName, IEnumerable<ILogSink> sinks, LoggerFactory factory)
    {
        _sinksArray = sinks.ToArray() ?? throw new ArgumentNullException(nameof(sinks));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _categoryName = categoryName;
        _queueCapacity = _factory.QueueCapacity;
        _queueFullMode = _factory.QueueFullMode;
        _syncWriteTimeout = _factory.SyncWriteTimeout;
        _channel = Channel.CreateBounded<LogEntry>(
            new BoundedChannelOptions(_queueCapacity)
            {
                FullMode = _queueFullMode switch
                {
                    LogQueueFullMode.DropOldest => BoundedChannelFullMode.DropOldest,
                    LogQueueFullMode.DropWrite => BoundedChannelFullMode.DropWrite,
                    LogQueueFullMode.Wait => BoundedChannelFullMode.Wait,
                    _ => BoundedChannelFullMode.DropOldest
                },
                SingleReader = true,
                AllowSynchronousContinuations = false
            }
        );
        _writer = _channel.Writer;
        _reader = _channel.Reader;
        _processingTask = Task.Run(async () => await ProcessEntries().ConfigureAwait(false));
        _fallbackSink = _sinksArray.FirstOrDefault(p => p is ConsoleSink or ColoredConsoleSink);
        _queueDepthProviderId = PicoLogMetrics.RegisterQueueDepthProvider(GetQueuedEntryCount);
    }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        if (_disposeState != 0 || !_factory.IsAcceptingWrites)
            return LoggerScopeProvider.Empty;

        return _factory.BeginScope(state);
    }

    public void Log(LogLevel logLevel, string message, Exception? exception = null) =>
        Write(logLevel, message, properties: null, exception);

    public void LogStructured(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties = null,
        Exception? exception = null
    ) => Write(logLevel, message, properties, exception);

    public Task LogAsync(
        LogLevel logLevel,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => WriteAsync(logLevel, message, properties: null, exception, cancellationToken);

    public Task LogStructuredAsync(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties = null,
        Exception? exception = null,
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
        HandleWriteResult(TryEnqueueSync(entry));
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

        var writeTask = TryEnqueueAsync(entry, cancellationToken);
        if (writeTask.IsCompletedSuccessfully)
        {
            HandleWriteResult(writeTask.Result);
            return Task.CompletedTask;
        }

        return AwaitWriteAsync(writeTask);

        async Task AwaitWriteAsync(ValueTask<LogWriteResult> pendingWrite)
        {
            HandleWriteResult(await pendingWrite.ConfigureAwait(false));
        }
    }

    private async ValueTask ProcessEntries()
    {
        while (await _reader.WaitToReadAsync().ConfigureAwait(false))
        {
            while (_reader.TryRead(out var entry))
            {
                Interlocked.Decrement(ref _queuedEntries);

                foreach (var sink in _sinksArray)
                {
                    try
                    {
                        await sink.WriteAsync(entry).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _factory.RecordSinkFailure();
                        await LogSinkErrorAsync(sink, ex, entry).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    private bool CanAcceptWrite(LogLevel logLevel)
    {
        if (_disposeState != 0 || !_factory.IsAcceptingWrites)
        {
            _factory.RecordRejectedAfterShutdown();
            return false;
        }

        return _factory.IsEnabled(logLevel);
    }

    private void HandleWriteResult(LogWriteResult result)
    {
        if (result == LogWriteResult.Dropped)
            ReportDroppedMessage();
        else if (result == LogWriteResult.RejectedAfterShutdown)
            _factory.RecordRejectedAfterShutdown();
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
            Category = _categoryName,
            Message = message,
            Exception = exception,
            Scopes = _factory.CaptureScopes(),
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

    private LogWriteResult TryEnqueueSync(LogEntry entry) =>
        _queueFullMode switch
        {
            LogQueueFullMode.Wait => TryEnqueueSyncWithWait(entry),
            LogQueueFullMode.DropWrite => TryEnqueueSyncDropWrite(entry),
            _ => TryEnqueueSyncDropOldest(entry)
        };

    private ValueTask<LogWriteResult> TryEnqueueAsync(LogEntry entry, CancellationToken cancellationToken) =>
        _queueFullMode switch
        {
            LogQueueFullMode.Wait => TryEnqueueAsyncWithWait(entry, cancellationToken),
            LogQueueFullMode.DropWrite => ValueTask.FromResult(TryEnqueueSyncDropWrite(entry)),
            _ => ValueTask.FromResult(TryEnqueueSyncDropOldest(entry))
        };

    private LogWriteResult TryEnqueueSyncDropOldest(LogEntry entry)
    {
        var wasAtCapacity = Volatile.Read(ref _queuedEntries) >= _queueCapacity;

        if (!_writer.TryWrite(entry))
            return DetermineFailedWriteResult();

        _factory.RecordEntryAccepted();

        if (wasAtCapacity)
            return LogWriteResult.Dropped;

        Interlocked.Increment(ref _queuedEntries);
        return LogWriteResult.Accepted;
    }

    private LogWriteResult TryEnqueueSyncDropWrite(LogEntry entry)
    {
        if (Volatile.Read(ref _queuedEntries) >= _queueCapacity)
            return LogWriteResult.Dropped;

        if (!_writer.TryWrite(entry))
            return DetermineFailedWriteResult();

        Interlocked.Increment(ref _queuedEntries);
        _factory.RecordEntryAccepted();
        return LogWriteResult.Accepted;
    }

    private LogWriteResult TryEnqueueSyncWithWait(LogEntry entry)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            while (true)
            {
                if (_writer.TryWrite(entry))
                {
                    Interlocked.Increment(ref _queuedEntries);
                    _factory.RecordEntryAccepted();
                    return LogWriteResult.Accepted;
                }

                var remaining = _syncWriteTimeout - Stopwatch.GetElapsedTime(startTimestamp);

                if (remaining <= TimeSpan.Zero)
                    return LogWriteResult.Dropped;

                var waitOperation = _writer.WaitToWriteAsync();

                if (waitOperation.IsCompletedSuccessfully)
                {
                    if (!waitOperation.Result)
                        return DetermineFailedWriteResult();

                    continue;
                }

                var waitTask = waitOperation.AsTask();

                if (!waitTask.Wait(remaining))
                    return LogWriteResult.Dropped;

                if (!waitTask.GetAwaiter().GetResult())
                    return DetermineFailedWriteResult();

                if (!_writer.TryWrite(entry))
                    continue;

                Interlocked.Increment(ref _queuedEntries);
                _factory.RecordEntryAccepted();
                return LogWriteResult.Accepted;
            }
        }
        catch (AggregateException ex) when (ex.InnerException is ChannelClosedException)
        {
            return DetermineFailedWriteResult();
        }
        catch (ChannelClosedException)
        {
            return DetermineFailedWriteResult();
        }
    }

    private async ValueTask<LogWriteResult> TryEnqueueAsyncWithWait(
        LogEntry entry,
        CancellationToken cancellationToken
    )
    {
        try
        {
            while (true)
            {
                if (_writer.TryWrite(entry))
                {
                    Interlocked.Increment(ref _queuedEntries);
                    _factory.RecordEntryAccepted();
                    return LogWriteResult.Accepted;
                }

                if (!await _writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false))
                    return DetermineFailedWriteResult();

                if (!_writer.TryWrite(entry))
                    continue;

                Interlocked.Increment(ref _queuedEntries);
                _factory.RecordEntryAccepted();
                return LogWriteResult.Accepted;
            }
        }
        catch (ChannelClosedException)
        {
            return DetermineFailedWriteResult();
        }
    }

    private LogWriteResult DetermineFailedWriteResult() =>
        _disposeState != 0 || !_factory.IsAcceptingWrites
            ? LogWriteResult.RejectedAfterShutdown
            : LogWriteResult.Dropped;

    private void ReportDroppedMessage()
    {
        var dropped = Interlocked.Increment(ref _droppedEntries);
        _factory.ReportDroppedMessages(_categoryName, dropped);
    }

    private async ValueTask LogSinkErrorAsync(
        ILogSink failingSink,
        Exception ex,
        LogEntry originalEntry
    )
    {
        if (_fallbackSink is null || ReferenceEquals(_fallbackSink, failingSink))
        {
            Debug.WriteLine($"Sink write error for '{originalEntry.Category}': {ex}");
            return;
        }

        var errorEntry = new LogEntry
        {
            Timestamp = GetTimestamp(),
            Level = LogLevel.Error,
            Category = nameof(InternalLogger),
            Message = $"Failed to write log entry to sink: {originalEntry.Message}",
            Exception = ex
        };

        try
        {
            await _fallbackSink.WriteAsync(errorEntry).ConfigureAwait(false);
        }
        catch (Exception fallbackException)
        {
            Debug.WriteLine($"Fallback sink write error: {fallbackException}");
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        try
        {
            _writer.TryComplete();
            await _processingTask.ConfigureAwait(false);
        }
        finally
        {
            PicoLogMetrics.UnregisterQueueDepthProvider(_queueDepthProviderId);
        }
    }

    private long GetQueuedEntryCount() => Volatile.Read(ref _queuedEntries);

    private static DateTimeOffset GetTimestamp() => TimeProvider.System.GetLocalNow();
}
