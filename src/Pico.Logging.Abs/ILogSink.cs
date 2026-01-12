namespace Pico.Logging.Abs;

public interface ILogSink : IDisposable, IAsyncDisposable
{
    ValueTask WriteAsync(LogEntry entry, CancellationToken cancellationToken = default);
}
