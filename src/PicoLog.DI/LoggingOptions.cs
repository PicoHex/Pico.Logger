namespace PicoLog.DI;

public sealed class LoggingOptions
{
    public ReadFromConfiguration ReadFrom { get; } = new();

    public SinkConfiguration WriteTo { get; } = new();

    public LogLevel MinLevel
    {
        get => Factory.MinLevel;
        set => Factory.MinLevel = value;
    }

    public bool UseColoredConsole { get; set; } = true;

    public bool EnableFileSink { get; set; }

    public LoggerFactoryOptions Factory { get; } = new();

    public FileSinkOptions File { get; } = new();

    public ILogFormatter Formatter
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(Formatter));
    } = new ConsoleFormatter();

    public string FilePath
    {
        get => File.FilePath;
        set
        {
            File.FilePath = value;
            EnableFileSink = true;
        }
    }

    internal LoggingOptions CreateValidatedCopy()
    {
        var hasExplicitFilePath = File.HasExplicitFilePath;
        var enableFileSink = EnableFileSink || hasExplicitFilePath;
        var copy = new LoggingOptions
        {
            UseColoredConsole = UseColoredConsole,
            EnableFileSink = enableFileSink,
            Formatter = Formatter
        };
        copy.ReadFrom.CopyFrom(ReadFrom);

        var factory = Factory.CreateValidatedCopy();
        copy.Factory.MinLevel = factory.MinLevel;
        copy.Factory.QueueCapacity = factory.QueueCapacity;
        copy.Factory.QueueFullMode = factory.QueueFullMode;
        copy.Factory.SyncWriteTimeout = factory.SyncWriteTimeout;
        copy.Factory.OnMessagesDropped = factory.OnMessagesDropped;

        CopyFileOptions(File, copy.File);

        foreach (var registration in WriteTo.Registrations)
        {
            if (registration.Kind is not SinkConfiguration.SinkKind.File)
            {
                copy.WriteTo.AddRegistration(registration);
                continue;
            }

            copy.WriteTo.AddRegistration(
                new SinkConfiguration.SinkRegistration(CreateValidatedFileOptions(registration.ConfigureFile))
            );
        }

        if (
            copy.ReadFrom.IncludeRegisteredSinks
            && !copy.WriteTo.HasRegistrations
            && !enableFileSink
        )
            return copy;

        if (
            !enableFileSink
            && !copy.WriteTo.Registrations.Any(
                registration => registration.Kind is SinkConfiguration.SinkKind.File
            )
        )
            return copy;

        if (enableFileSink)
            CopyFileOptions(CreateValidatedFileOptions(), copy.File);

        return copy;
    }

    internal FileSinkOptions CreateValidatedFileOptions(Action<FileSinkOptions>? configure = null)
    {
        var fileOptions = new FileSinkOptions
        {
            BatchSize = File.BatchSize,
            QueueCapacity = File.QueueCapacity,
            FlushInterval = File.FlushInterval
        };

        if (File.HasExplicitFilePath)
            fileOptions.FilePath = File.FilePath;

        configure?.Invoke(fileOptions);

        if (!fileOptions.HasExplicitFilePath)
            throw new InvalidOperationException(
                "EnableFileSink requires an explicitly configured FilePath."
            );

        return fileOptions.CreateValidatedCopy();
    }

    private static void CopyFileOptions(FileSinkOptions source, FileSinkOptions destination)
    {
        if (source.HasExplicitFilePath)
            destination.FilePath = source.FilePath;

        destination.BatchSize = source.BatchSize;
        destination.QueueCapacity = source.QueueCapacity;
        destination.FlushInterval = source.FlushInterval;
    }
}
