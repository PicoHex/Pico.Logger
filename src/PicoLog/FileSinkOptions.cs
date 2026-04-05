namespace PicoLog;

public sealed class FileSinkOptions
{
    public string FilePath { get; set; } = "logs/test.log";

    public int BatchSize { get; set; } = 32;

    public int QueueCapacity { get; set; } = 4096;

    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    public FileSinkOptions CreateValidatedCopy()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(FilePath);

        if (BatchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(BatchSize));

        if (QueueCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(QueueCapacity));

        if (FlushInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(FlushInterval));

        return new FileSinkOptions
        {
            FilePath = FilePath,
            BatchSize = BatchSize,
            QueueCapacity = QueueCapacity,
            FlushInterval = FlushInterval
        };
    }
}
