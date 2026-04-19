namespace PicoLog;

public sealed class ColoredConsoleSink(ILogFormatter formatter, TextWriter? writer = null)
    : IConsoleFallbackSink
{
    private readonly ILogFormatter _formatter =
        formatter ?? throw new ArgumentNullException(nameof(formatter));
    private readonly TextWriter _writer = writer ?? Console.Out;

    public Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        var message = _formatter.Format(entry);
        return ConsoleSinkWriter.WriteAsync(
            _writer,
            message,
            entry.Level,
            static (writer, text, level) =>
            {
                var originalColor = Console.ForegroundColor;

                try
                {
                    Console.ForegroundColor = level switch
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

                    writer.WriteLine(text);
                }
                finally
                {
                    Console.ForegroundColor = originalColor;
                }
            }
        );
    }

    public void Dispose() { }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
