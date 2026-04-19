# Source Projects

This folder contains the three source packages tracked by `PicoLog.slnx`:

- `PicoLog.Abs`: the public contract package, including `ILogger`, `ILogger<T>`, `ILoggerFactory`, `LogLevel`, `ILogSink`, `ILogFormatter`, `LogEntry`, and flush companion contracts
- `PicoLog`: the core runtime implementation, including `LoggerFactory`, `Logger<T>`, built-in sinks, built-in formatters, and queueing/runtime behavior
- `PicoLog.DI`: PicoDI integration through `AddPicoLog(...)`

In short, `PicoLog.Abs` defines the shared public contract surface, `PicoLog` implements that runtime, and `PicoLog.DI` wires the runtime into PicoDI.

For installation, configuration, usage examples, and the full repository overview, see the root [README](../README.md).
