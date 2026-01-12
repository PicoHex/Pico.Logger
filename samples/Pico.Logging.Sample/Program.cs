// Initialize DI container and configure logging

var container = new SvcContainer();

// Register logging services - 只需调用 AddLogging() 即可
container.AddLogging();
container.ConfigureServices();

await using var scope = container.CreateScope();

// Create typed logger instance
var service = scope.GetService<IService>();

await service.WriteLogAsync();

// Demonstrate basic async logging

Console.ReadKey();
