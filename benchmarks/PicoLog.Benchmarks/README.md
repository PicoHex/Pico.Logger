# PicoLog.Benchmarks

Run the benchmark project in `Release` mode.

## Run commands

- Run both suites:
  - `dotnet run -c Release --project benchmarks/PicoLog.Benchmarks`
- Run only the main suite:
  - `dotnet run -c Release --project benchmarks/PicoLog.Benchmarks -- main`
- Run only the wait suite:
  - `dotnet run -c Release --project benchmarks/PicoLog.Benchmarks -- wait`

## Suites

- `main` runs `LoggingBenchmarks`
- `wait` runs `WaitLoggingBenchmarks`

The wait suite is intentionally small and local-friendly:

- `PicoWaitControl_CachedMessage` is the lightweight wait-mode control case
- `PicoWaitHandoff_CachedMessage` adds forced backpressure through `BackpressureSink`

## Results

The benchmark app writes `benchmark-results.md` next to the executable in the output directory.

Interpret wait-suite numbers as relative backpressure comparisons, not absolute throughput claims.
