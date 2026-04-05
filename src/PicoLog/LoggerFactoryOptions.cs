namespace PicoLog;

public sealed class LoggerFactoryOptions
{
    public LogLevel MinLevel { get; set; } = LogLevel.Debug;

    public int QueueCapacity { get; set; } = 65535;

    public LogQueueFullMode QueueFullMode { get; set; } = LogQueueFullMode.DropOldest;

    public TimeSpan SyncWriteTimeout { get; set; } = TimeSpan.FromMilliseconds(250);

    public Action<string, long>? OnMessagesDropped { get; set; }

    public LoggerFactoryOptions CreateValidatedCopy()
    {
        if (QueueCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(QueueCapacity));

        if (SyncWriteTimeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(SyncWriteTimeout));

        return new LoggerFactoryOptions
        {
            MinLevel = MinLevel,
            QueueCapacity = QueueCapacity,
            QueueFullMode = QueueFullMode,
            SyncWriteTimeout = SyncWriteTimeout,
            OnMessagesDropped = OnMessagesDropped
        };
    }
}
