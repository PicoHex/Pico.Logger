# Tests

This folder contains automated tests for the PicoLog solution.

- `PicoLog.Tests` uses TUnit on Microsoft Testing Platform and covers logger lifecycle, DI integration, file sink persistence, filtering, scope capture, and sink fault handling.
- `AssemblySurfaceTests` pin the boundary so `PicoLog.Abs` stays consumer-facing while runtime/extensibility contracts live in the `PicoLog` namespace and assembly.
