namespace PicoLog.DI;

public sealed class SinkConfiguration
{
    private readonly List<SinkRegistration> _registrations = [];

    internal IReadOnlyList<SinkRegistration> Registrations => _registrations;

    internal bool HasRegistrations => _registrations.Count != 0;

    public SinkConfiguration Console()
    {
        _registrations.Add(new SinkRegistration(SinkKind.Console));
        return this;
    }

    public SinkConfiguration ColoredConsole()
    {
        _registrations.Add(new SinkRegistration(SinkKind.ColoredConsole));
        return this;
    }

    public SinkConfiguration File()
    {
        _registrations.Add(new SinkRegistration(SinkKind.File));
        return this;
    }

    public SinkConfiguration File(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _registrations.Add(
            new SinkRegistration(SinkKind.File, configureFile: options => options.FilePath = path)
        );
        return this;
    }

    public SinkConfiguration File(Action<FileSinkOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _registrations.Add(new SinkRegistration(SinkKind.File, configureFile: configure));
        return this;
    }

    public SinkConfiguration Sink(ILogSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        _registrations.Add(new SinkRegistration(_ => sink));
        return this;
    }

    public SinkConfiguration Sink(Func<ILogSink> sinkFactory)
    {
        ArgumentNullException.ThrowIfNull(sinkFactory);

        _registrations.Add(new SinkRegistration(_ => sinkFactory()));
        return this;
    }

    public SinkConfiguration Sink(Func<ILogFormatter, ILogSink> sinkFactory)
    {
        ArgumentNullException.ThrowIfNull(sinkFactory);

        _registrations.Add(new SinkRegistration(sinkFactory));
        return this;
    }

    internal void AddRegistration(SinkRegistration registration) =>
        _registrations.Add(registration);

    internal enum SinkKind
    {
        Console,
        ColoredConsole,
        File,
        Custom
    }

    internal readonly record struct SinkRegistration(
        SinkKind Kind,
        Func<ILogFormatter, ILogSink>? CreateSink = null,
        Action<FileSinkOptions>? ConfigureFile = null,
        FileSinkOptions? FileOptions = null
    )
    {
        public SinkRegistration(Func<ILogFormatter, ILogSink> createSink)
            : this(SinkKind.Custom, createSink, null, null) { }

        public SinkRegistration(SinkKind kind, Action<FileSinkOptions>? configureFile)
            : this(kind, null, configureFile, null) { }

        public SinkRegistration(FileSinkOptions fileOptions)
            : this(SinkKind.File, null, null, fileOptions) { }
    }
}
