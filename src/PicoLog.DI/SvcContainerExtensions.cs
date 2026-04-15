namespace PicoLog.DI;

public static class SvcContainerExtensions
{
    extension(ISvcContainer container)
    {
        public ISvcContainer AddLogging(Action<LoggingOptions> configure
        )
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(configure);

            var options = new LoggingOptions();
            configure(options);
            LoggingOptions snapshot = options.CreateValidatedCopy();

            container
                .Register(
                    new SvcDescriptor(
                        typeof(ILoggerFactory),
                        _ => CreateLoggerFactory(snapshot)
                    )
                )
                .RegisterSingleton(typeof(ILogger<>), typeof(Logger<>))
                .RegisterSingleton(typeof(IStructuredLogger<>), typeof(Logger<>));

            return container;
        }

        public ISvcContainer AddLogging(LogLevel minLevel = LogLevel.Debug,
            string? filePath = null
        ) =>
            container.AddLogging(options =>
                {
                    options.MinLevel = minLevel;

                    if (filePath is not null)
                        options.FilePath = filePath;
                }
            );
    }

    private static LoggerFactory CreateLoggerFactory(LoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var formatter = new ConsoleFormatter();
        ILogSink consoleSink = options.UseColoredConsole
            ? new ColoredConsoleSink(formatter)
            : new ConsoleSink(formatter);
        List<ILogSink> sinks = [consoleSink];

        if (options.EnableFileSink)
            sinks.Add(new FileSink(formatter, options.File));

        return new LoggerFactory(sinks, options.Factory);
    }
}
