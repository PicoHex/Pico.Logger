namespace PicoLog.DI;

public static class SvcContainerExtensions
{
    extension(ISvcContainer container)
    {
        public ISvcContainer AddPicoLog(Action<LoggingOptions> configure) =>
            AddPicoLogCore(container, configure);

        public ISvcContainer AddPicoLog(
            LogLevel minLevel = LogLevel.Debug,
            string? filePath = null
        ) =>
            container.AddPicoLog(options =>
                {
                    options.MinLevel = minLevel;

                    if (filePath is not null)
                        options.FilePath = filePath;
                }
            );
    }

    private static ISvcContainer AddPicoLogCore(
        ISvcContainer container,
        Action<LoggingOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new LoggingOptions();
        configure(options);
        LoggingOptions snapshot = options.CreateValidatedCopy();
        var sync = new Lock();
        ILoggerFactory? factory = null;

        ILoggerFactory ResolveFactory()
        {
            lock (sync)
                return factory ??= CreateLoggerFactory(container, snapshot);
        }

        var registrations = container
            .Register(new SvcDescriptor(typeof(ILoggerFactory), _ => ResolveFactory()))
            .RegisterSingleton(typeof(ILogger<>), typeof(Logger<>));

        return registrations;
    }

    private static ILoggerFactory CreateLoggerFactory(ISvcContainer container, LoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.ReadFrom.IncludeRegisteredSinks)
            return new LoggerFactory(CreateOwnedSinks(options, includeLegacyDefaults: true), options.Factory);

        var loggingScope = container.CreateScope();
        try
        {
            var sinks = ResolveRegisteredSinks(loggingScope);
            sinks.AddRange(CreateOwnedSinks(options, includeLegacyDefaults: false));

            if (sinks.Count == 0)
            {
                loggingScope.Dispose();
                throw new InvalidOperationException(
                    "ReadFrom.RegisteredSinks requires at least one registered ILogSink when no explicit WriteTo sinks are configured."
                );
            }

            return new OwnedLoggerFactory(new LoggerFactory(sinks, options.Factory), loggingScope);
        }
        catch
        {
            loggingScope.Dispose();
            throw;
        }
    }

    private static List<ILogSink> ResolveRegisteredSinks(ISvcScope scope)
    {
        try
        {
            return scope.GetServices<ILogSink>().Select(NonOwningLogSink.Wrap).ToList();
        }
        catch (Exception ex) when (IsMissingRegisteredSinksException(ex))
        {
            return [];
        }
    }

    private static bool IsMissingRegisteredSinksException(Exception exception) =>
        exception.GetType().Name is "PicoDiException"
        && exception.Message.Contains("PicoLog.ILogSink")
        && exception.Message.Contains("is not registered", StringComparison.Ordinal);

    private static List<ILogSink> CreateOwnedSinks(LoggingOptions options, bool includeLegacyDefaults)
    {
        ILogFormatter formatter = options.Formatter;
        List<ILogSink> sinks = [];

        if (options.WriteTo.HasRegistrations)
        {
            foreach (var registration in options.WriteTo.Registrations)
            {
                switch (registration.Kind)
                {
                    case SinkConfiguration.SinkKind.Console:
                        sinks.Add(new ConsoleSink(formatter));
                        break;

                    case SinkConfiguration.SinkKind.ColoredConsole:
                        sinks.Add(new ColoredConsoleSink(formatter));
                        break;

                    case SinkConfiguration.SinkKind.File:
                        sinks.Add(new FileSink(formatter, registration.FileOptions!));
                        break;

                    case SinkConfiguration.SinkKind.Custom:
                        sinks.Add(registration.CreateSink!(formatter));
                        break;
                }
            }
        }
        else if (includeLegacyDefaults)
        {
            ILogSink consoleSink = options.UseColoredConsole
                ? new ColoredConsoleSink(formatter)
                : new ConsoleSink(formatter);
            sinks.Add(consoleSink);

            if (options.EnableFileSink)
                sinks.Add(new FileSink(formatter, options.File));
        }

        return sinks;
    }
}
