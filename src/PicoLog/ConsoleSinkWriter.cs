namespace PicoLog;

internal static class ConsoleSinkWriter
{
    public static Task WriteAsync(TextWriter writer, string message)
    {
        if (!ReferenceEquals(writer, Console.Out))
        {
            writer.WriteLine(message);
            return Task.CompletedTask;
        }

        lock (ConsoleWriteCoordinator.OutputLock)
            writer.WriteLine(message);

        return Task.CompletedTask;
    }

    public static Task WriteAsync<TState>(
        TextWriter writer,
        string message,
        TState state,
        Action<TextWriter, string, TState> consoleWrite
    )
    {
        if (!ReferenceEquals(writer, Console.Out))
        {
            writer.WriteLine(message);
            return Task.CompletedTask;
        }

        lock (ConsoleWriteCoordinator.OutputLock)
            consoleWrite(writer, message, state);

        return Task.CompletedTask;
    }
}
