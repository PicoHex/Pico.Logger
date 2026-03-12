namespace Pico.Logging;

public sealed class ConsoleSink : ILogSink
{
    private readonly ILogFormatter _formatter;
    private readonly TextWriter _writer;

    public ConsoleSink(ILogFormatter formatter, TextWriter? writer = null)
    {
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _writer = writer ?? Console.Out;
    }

    public ValueTask WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        _writer.WriteLine(_formatter.Format(entry));
        return ValueTask.CompletedTask;
    }

    public void Dispose() { }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
