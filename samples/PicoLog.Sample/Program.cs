// Set up the container and the default logging pipeline.

ISvcContainer container = new SvcContainer();

container
    .AddLogging(options =>
        {
            options.MinLevel = LogLevel.Debug;
            options.UseColoredConsole = true;
            options.FilePath = "logs/app.log";
        }
    )
    .ConfigureServices();

await using var scope = container.CreateScope();
var loggerFactory = scope.GetService<ILoggerFactory>();

// Run the sample workload.
var service = scope.GetService<IService>();

await service.WriteLogAsync();

await loggerFactory.DisposeAsync();

// The explicit disposal flushes queued log entries before exit.
