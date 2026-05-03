using PicoBench;
using PicoLog;
using PicoLog.Abs;
using PicoLogLevel = PicoLog.Abs.LogLevel;

namespace PicoLog.Benchmarks;

[BenchmarkClass(Description = "PicoLog formatting overhead — ConsoleSink / FileSink / multi-sink")]
public partial class FormattingBenchmarks
{
    private const int QueueCapacity = 262144;
    private static readonly string CachedMessage = "Hello from benchmark, cached message";

    private PicoLog.Abs.ILogger _picoNullLogger = null!;
    private PicoLog.LoggerFactory _picoNullFactory = null!;

    private PicoLog.Abs.ILogger _picoConsoleLogger = null!;
    private PicoLog.LoggerFactory _picoConsoleFactory = null!;

    private PicoLog.Abs.ILogger _picoPooledLogger = null!;
    private PicoLog.LoggerFactory _picoPooledFactory = null!;

    private PicoLog.Abs.ILogger _picoFileLogger = null!;
    private PicoLog.LoggerFactory _picoFileFactory = null!;
    private string _filePath = null!;

    private PicoLog.Abs.ILogger _picoDualLogger = null!;
    private PicoLog.LoggerFactory _picoDualFactory = null!;
    private string _dualFilePath = null!;

    [Params(100, 200)]
    public int N { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _picoNullFactory = new LoggerFactory(
            [new NullSink()],
            new LoggerFactoryOptions { MinLevel = PicoLogLevel.Trace, QueueCapacity = QueueCapacity }
        );
        _picoNullLogger = _picoNullFactory.CreateLogger("Benchmark");

        _picoConsoleFactory = new LoggerFactory(
            [new ConsoleSink(new ConsoleFormatter(), TextWriter.Null)],
            new LoggerFactoryOptions { MinLevel = PicoLogLevel.Trace, QueueCapacity = QueueCapacity }
        );
        _picoConsoleLogger = _picoConsoleFactory.CreateLogger("Benchmark");

        _picoPooledFactory = new LoggerFactory(
            [new ConsoleSink(new PooledConsoleFormatter(), TextWriter.Null)],
            new LoggerFactoryOptions { MinLevel = PicoLogLevel.Trace, QueueCapacity = QueueCapacity }
        );
        _picoPooledLogger = _picoPooledFactory.CreateLogger("Benchmark");

        _filePath = Path.Combine(Path.GetTempPath(), $"picolog-bench-{Guid.NewGuid():N}.log");
        _picoFileFactory = new LoggerFactory(
            [new FileSink(new ConsoleFormatter(), new FileSinkOptions
            {
                FilePath = _filePath,
                BatchSize = 32,
                FlushInterval = TimeSpan.Zero
            })],
            new LoggerFactoryOptions { MinLevel = PicoLogLevel.Trace, QueueCapacity = QueueCapacity }
        );
        _picoFileLogger = _picoFileFactory.CreateLogger("Benchmark");

        _dualFilePath = Path.Combine(Path.GetTempPath(), $"picolog-bench-dual-{Guid.NewGuid():N}.log");
        _picoDualFactory = new LoggerFactory(
            [
                new ConsoleSink(new ConsoleFormatter(), TextWriter.Null),
                new FileSink(new ConsoleFormatter(), new FileSinkOptions
                {
                    FilePath = _dualFilePath,
                    BatchSize = 32,
                    FlushInterval = TimeSpan.Zero
                })
            ],
            new LoggerFactoryOptions { MinLevel = PicoLogLevel.Trace, QueueCapacity = QueueCapacity }
        );
        _picoDualLogger = _picoDualFactory.CreateLogger("Benchmark");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _picoNullFactory.Dispose();
        _picoConsoleFactory.Dispose();
        _picoPooledFactory.Dispose();
        _picoFileFactory.Dispose();
        _picoDualFactory.Dispose();

        TryDelete(_filePath);
        TryDelete(_dualFilePath);
    }

    [Benchmark(Description = "PicoLog: NullSink (no formatting)")]
    public void PicoNullSink()
    {
        for (var i = 0; i < N; i++)
            _picoNullLogger.Log(PicoLogLevel.Info, CachedMessage);
    }

    [Benchmark(Description = "PicoLog: ConsoleSink → TextWriter.Null")]
    public void PicoConsoleSink()
    {
        for (var i = 0; i < N; i++)
            _picoConsoleLogger.Log(PicoLogLevel.Info, CachedMessage);
    }

    [Benchmark(Description = "PicoLog: PooledConsoleFormatter → TextWriter.Null")]
    public void PicoPooledConsole()
    {
        for (var i = 0; i < N; i++)
            _picoPooledLogger.Log(PicoLogLevel.Info, CachedMessage);
    }

    [Benchmark(Description = "PicoLog: FileSink (format + batch I/O)")]
    public void PicoFileSink()
    {
        for (var i = 0; i < N; i++)
            _picoFileLogger.Log(PicoLogLevel.Info, CachedMessage);
    }

    [Benchmark(Description = "PicoLog: ConsoleSink + FileSink (dual)")]
    public void PicoDualSink()
    {
        for (var i = 0; i < N; i++)
            _picoDualLogger.Log(PicoLogLevel.Info, CachedMessage);
    }

    private static void TryDelete(string? path)
    {
        try { if (path is not null) File.Delete(path); }
        catch { }
    }
}
