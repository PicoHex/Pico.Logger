# Tests

This folder contains automated tests for the PicoLog solution.

- `PicoLog.Tests` uses TUnit on Microsoft Testing Platform and covers logger lifecycle, DI integration, file sink persistence, filtering, scope capture, and sink fault handling.
- `AssemblySurfaceTests` pin the boundary so `PicoLog.Abs` owns the public logging contract surface while `PicoLog` stays focused on runtime implementation types.
