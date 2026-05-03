namespace PicoLog.DI;

internal static class NonOwningLogSink
{
    public static ILogSink Wrap(ILogSink sink) =>
        sink is IFlushableLogSink flushableSink
            ? new NonOwningFlushableSink(flushableSink)
            : new NonOwningSink(sink);

    private sealed class NonOwningSink(ILogSink innerSink) : ILogSink
    {
        public Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default) =>
            innerSink.WriteAsync(entry, cancellationToken);

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NonOwningFlushableSink(IFlushableLogSink innerSink) : IFlushableLogSink
    {
        public Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default) =>
            innerSink.WriteAsync(entry, cancellationToken);

        public ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
            innerSink.FlushAsync(cancellationToken);

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
