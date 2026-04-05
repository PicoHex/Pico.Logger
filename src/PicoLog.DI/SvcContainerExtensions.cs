namespace PicoLog.DI;

public static class SvcContainerExtensions
{
    public static PicoDI.Abs.ISvcContainer AddLogging(
        this PicoDI.Abs.ISvcContainer container,
        Action<LoggingOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new LoggingOptions();
        configure(options);
        var snapshot = options.CreateValidatedCopy();

        container
            .Register(
                new PicoDI.Abs.SvcDescriptor(
                    typeof(ILoggerFactory),
                    _ => CreateLoggerFactory(snapshot),
                    PicoDI.Abs.SvcLifetime.Singleton
                )
            )
            .RegisterSingleton(typeof(ILogger<>), typeof(Logger<>));

        return container;
    }

    public static PicoDI.Abs.ISvcContainer AddLogging(
        this PicoDI.Abs.ISvcContainer container,
        LogLevel minLevel = LogLevel.Debug,
        string filePath = "logs/test.log"
    ) =>
        AddLogging(
            container,
            options =>
            {
                options.MinLevel = minLevel;
                options.FilePath = filePath;
            }
        );

    private static LoggerFactory CreateLoggerFactory(LoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var formatter = new ConsoleFormatter();
        options.Factory.MinLevel = options.MinLevel;
        ILogSink consoleSink = options.UseColoredConsole
            ? new ColoredConsoleSink(formatter)
            : new ConsoleSink(formatter);

        return new LoggerFactory(
            [consoleSink, new FileSink(formatter, options.File)],
            options.Factory
        );
    }
}
