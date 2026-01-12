namespace Pico.Logging.DI;

public static class SvcContainerExtensions
{
    public static ISvcContainer AddLogging(this ISvcContainer container)
    {
        container
            .Register(new SvcDescriptor(
                typeof(ILogFormatter), 
                static _ => new ConsoleFormatter(),
                SvcLifetime.Singleton))
            .Register(new SvcDescriptor(
                typeof(ILogSink),
                static s => new ConsoleSink((ILogFormatter)s.GetService(typeof(ILogFormatter))),
                SvcLifetime.Singleton))
            .Register(new SvcDescriptor(
                typeof(ILogSink),
                static s => new FileSink((ILogFormatter)s.GetService(typeof(ILogFormatter))),
                SvcLifetime.Singleton))
            // 添加 IEnumerable<ILogSink> 注册
            .Register(new SvcDescriptor(
                typeof(IEnumerable<ILogSink>),
                static s => new ILogSink[]
                {
                    new ConsoleSink((ILogFormatter)s.GetService(typeof(ILogFormatter))),
                    new FileSink((ILogFormatter)s.GetService(typeof(ILogFormatter)))
                },
                SvcLifetime.Singleton))
            .Register(new SvcDescriptor(
                typeof(ILoggerFactory),
                static s => new LoggerFactory(
                    (IEnumerable<ILogSink>)s.GetService(typeof(IEnumerable<ILogSink>))),
                SvcLifetime.Singleton))
            .RegisterSingleton(typeof(ILogger<>), typeof(Logger<>));
        return container;
    }
}
