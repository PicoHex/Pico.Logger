namespace PicoLog.DI;

public sealed class LoggingOptions
{
    private bool _hasExplicitFilePath;

    public LogLevel MinLevel
    {
        get => Factory.MinLevel;
        set => Factory.MinLevel = value;
    }

    public bool UseColoredConsole { get; set; } = true;

    public bool EnableFileSink { get; set; }

    public LoggerFactoryOptions Factory { get; } = new();

    public FileSinkOptions File { get; } = new();

    public string FilePath
    {
        get => File.FilePath;
        set
        {
            File.FilePath = value;
            _hasExplicitFilePath = true;
            EnableFileSink = true;
        }
    }

    internal LoggingOptions CreateValidatedCopy()
    {
        var hasExplicitFilePath = HasExplicitFilePath();
        var enableFileSink = EnableFileSink || hasExplicitFilePath;
        var copy = new LoggingOptions
        {
            UseColoredConsole = UseColoredConsole,
            EnableFileSink = enableFileSink,
            _hasExplicitFilePath = _hasExplicitFilePath
        };

        var factory = Factory.CreateValidatedCopy();
        copy.Factory.MinLevel = factory.MinLevel;
        copy.Factory.QueueCapacity = factory.QueueCapacity;
        copy.Factory.QueueFullMode = factory.QueueFullMode;
        copy.Factory.SyncWriteTimeout = factory.SyncWriteTimeout;
        copy.Factory.OnMessagesDropped = factory.OnMessagesDropped;

        if (!enableFileSink)
            return copy;

        if (!hasExplicitFilePath)
            throw new InvalidOperationException(
                "EnableFileSink requires an explicitly configured FilePath."
            );

        var file = File.CreateValidatedCopy();
        copy.File.FilePath = file.FilePath;
        copy.File.BatchSize = file.BatchSize;
        copy.File.QueueCapacity = file.QueueCapacity;
        copy.File.FlushInterval = file.FlushInterval;

        return copy;
    }

    private bool HasExplicitFilePath() =>
        _hasExplicitFilePath || File.HasExplicitFilePath;
}
