namespace Pico.Logging.DI;

public static class SvcContainerExtensions
{
    public static ISvcContainer AddLogging(
        this ISvcContainer container,
        LogLevel minLevel = LogLevel.Debug,
        string filePath = "logs/test.log"
    )
    {
        container
            .Register(
                new SvcDescriptor(
                    typeof(ILoggerFactory),
                    _ => CreateLoggerFactory(minLevel, filePath),
                    SvcLifetime.Singleton
                )
            )
            .RegisterSingleton(typeof(ILogger<>), typeof(Logger<>));
        return container;
    }

    private static LoggerFactory CreateLoggerFactory(LogLevel minLevel, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var formatter = new ConsoleFormatter();

        // The factory owns the sinks it creates and disposes them during shutdown.
        return new LoggerFactory([new ConsoleSink(formatter), new FileSink(formatter, filePath)])
        {
            MinLevel = minLevel
        };
    }
}
