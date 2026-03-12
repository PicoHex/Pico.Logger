namespace Pico.Logging;

public sealed class ColoredConsoleSink : ILogSink
{
    private static readonly Lock ConsoleLock = new();
    private readonly ILogFormatter _formatter;
    private readonly TextWriter _writer;

    public ColoredConsoleSink(ILogFormatter formatter, TextWriter? writer = null)
    {
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _writer = writer ?? Console.Out;
    }

    public ValueTask WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        var message = _formatter.Format(entry);

        if (!ReferenceEquals(_writer, Console.Out))
        {
            _writer.WriteLine(message);
            return ValueTask.CompletedTask;
        }

        lock (ConsoleLock)
        {
            var originalColor = Console.ForegroundColor;

            try
            {
                Console.ForegroundColor = entry.Level switch
                {
                    LogLevel.Trace => ConsoleColor.Gray,
                    LogLevel.Debug => ConsoleColor.Cyan,
                    LogLevel.Info => ConsoleColor.Green,
                    LogLevel.Notice => ConsoleColor.Blue,
                    LogLevel.Warning => ConsoleColor.Yellow,
                    LogLevel.Error => ConsoleColor.Red,
                    LogLevel.Critical => ConsoleColor.DarkRed,
                    LogLevel.Alert => ConsoleColor.Magenta,
                    LogLevel.Emergency => ConsoleColor.DarkMagenta,
                    _ => originalColor
                };

                _writer.WriteLine(message);
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }

        return ValueTask.CompletedTask;
    }

    public void Dispose() { }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
