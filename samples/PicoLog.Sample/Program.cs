// Set up the container and the default logging pipeline.

ISvcContainer container = new SvcContainer();

container
    .AddPicoLog(options =>
        {
            options.MinLevel = LogLevel.Debug;
            options.WriteTo.ColoredConsole();
            options.WriteTo.File("logs/app.log");
        }
    )
    .ConfigureServices();

await using var scope = container.CreateScope();
var logControl = scope.GetService<IPicoLogControl>();

// Run the sample workload.
var service = scope.GetService<IService>();

await service.WriteLogAsync();

await logControl.DisposeAsync();

// The explicit disposal flushes queued log entries before exit.
