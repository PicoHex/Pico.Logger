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

    public LoggerFactoryOptions Factory { get; } = new();

    public FileSinkOptions File { get; } = new();

    public ILogFormatter Formatter
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(Formatter));
    } = new ConsoleFormatter();

    internal LoggingOptions CreateValidatedCopy()
    {
        var copy = new LoggingOptions
        {
            Formatter = Formatter
        };
        copy.ReadFrom.CopyFrom(ReadFrom);

        var factory = Factory.CreateValidatedCopy();
        copy.Factory.MinLevel = factory.MinLevel;
        copy.Factory.QueueCapacity = factory.QueueCapacity;
        copy.Factory.QueueFullMode = factory.QueueFullMode;
        copy.Factory.SyncWriteTimeout = factory.SyncWriteTimeout;
        copy.Factory.OnMessagesDropped = factory.OnMessagesDropped;

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
                "WriteTo.File requires an explicitly configured FilePath."
            );

        return fileOptions.CreateValidatedCopy();
    }


}
