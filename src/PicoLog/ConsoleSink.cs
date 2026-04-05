namespace PicoLog;

public sealed class ConsoleSink(ILogFormatter formatter, TextWriter? writer = null) : ILogSink
{
    private readonly ILogFormatter _formatter =
        formatter ?? throw new ArgumentNullException(nameof(formatter));
    private readonly TextWriter _writer = writer ?? Console.Out;

    public Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        var message = _formatter.Format(entry);

        if (!ReferenceEquals(_writer, Console.Out))
        {
            _writer.WriteLine(message);
            return Task.CompletedTask;
        }

        lock (ConsoleWriteCoordinator.OutputLock)
            _writer.WriteLine(message);

        return Task.CompletedTask;
    }

    public void Dispose() { }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
