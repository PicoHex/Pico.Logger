namespace Pico.Logging.DI;

public sealed class LoggingOptions
{
    public LogLevel MinLevel { get; set; } = LogLevel.Debug;

    public bool UseColoredConsole { get; set; } = true;

    public LoggerFactoryOptions Factory { get; } = new();

    public FileSinkOptions File { get; } = new();

    public string FilePath
    {
        get => File.FilePath;
        set => File.FilePath = value;
    }

    internal LoggingOptions CreateValidatedCopy()
    {
        var copy = new LoggingOptions
        {
            MinLevel = MinLevel,
            UseColoredConsole = UseColoredConsole,
            FilePath = FilePath
        };

        var factory = Factory.CreateValidatedCopy();
        copy.Factory.MinLevel = factory.MinLevel;
        copy.Factory.QueueCapacity = factory.QueueCapacity;
        copy.Factory.QueueFullMode = factory.QueueFullMode;
        copy.Factory.SyncWriteTimeout = factory.SyncWriteTimeout;
        copy.Factory.OnMessagesDropped = factory.OnMessagesDropped;

        var file = File.CreateValidatedCopy();
        copy.File.FilePath = file.FilePath;
        copy.File.BatchSize = file.BatchSize;
        copy.File.QueueCapacity = file.QueueCapacity;
        copy.File.FlushInterval = file.FlushInterval;

        return copy;
    }
}
