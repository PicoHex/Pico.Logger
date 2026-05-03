# Perf Baseline — 2026-05

- **Baseline commit:** `6b850c2`
- **Suite:** `benchmarks/PicoLog.Benchmarks` (`LoggingBenchmarks`, formatting suite)
- **Total wall time:** ~644s
- **Configurations:** N=100 and N=200 entries per iteration
- **Sinks measured:** `PicoNullSink`, `PicoConsoleSink`, `PicoPooledConsole`, `PicoFileSink`, `PicoDualSink`

This document pins the current performance position of PicoLog and records two
explicitly **rejected** optimization proposals so they don't get re-litigated
in future iterations. Any future perf proposal must either beat these numbers
or argue why the trade-off the data captured here is no longer accurate.

## Headline numbers

Per-entry cost on the formatting path is ~400–500 ns. The dominant
contributors are, in order:

1. `LogEntry` heap allocation (~1.4 Gen0 collections per entry — see
   `src/PicoLog.Abs/LogEntry.cs`, currently a `sealed record` class)
2. `Channel<LogEntry>` async coordination
3. Per-call `StringBuilder` allocation in the formatting sinks (~64 B,
   contributes ~0.2 Gen0/entry)
4. The `lock` taken on the synchronous fast-path (~20–30 ns)

Items (3) and (4) below are the rejected proposals.

### Note on `PicoNullSink` vs `PicoConsoleSink`

`PicoNullSink` reports a *higher* mean than `PicoConsoleSink` in the raw
table. This is a measurement-window artifact, not a real regression:
`NullSink` drains the channel so fast that more entries pile up inside the
same iteration window, which triggers more Gen0 collections to be billed
against that iteration. The relative comparisons between sinks are still
valid; the absolute null-sink number is not a meaningful "floor".

## Rejected proposal #7 — Pooled `StringBuilder` in formatting sinks

**Direct quantification (`PicoConsoleSink` vs `PicoPooledConsole`, N=100):**

| Metric  | Change vs `PicoConsoleSink` |
| ------- | ---------------------------- |
| Gen0 GC | **−17–18%** (~788 fewer Gen0 collections / 100 entries) |
| Gen1 GC | reduced                       |
| Gen2 GC | reduced                       |
| Wall time | **within ±5%** (reverses sign at N=200) |

**Decision: do not adopt.**

The Gen0 reduction is real and reproducible — it is *not* noise. But the
~20–40 ns/entry that this saves does not surface above the wall-time noise
floor, because it is dominated by the `LogEntry` allocation and channel
coordination upstream. The 64 B `StringBuilder` is dwarfed by the per-entry
`LogEntry` (~1.4 Gen0/entry, see baseline section above).

Engineering cost of pooling — thread-local pool, capacity cap, reset
semantics, leak-on-throw discipline — buys ~2–3% of the total pipeline.
**Negative ROI**; closing.

## Rejected proposal #1 — Lock-free synchronous fast-path

The current synchronous path takes a `lock` that costs ~20–30 ns, which is
~5–6% of the 400–500 ns/entry total. A lock-free variant would have to use
volatile / interlocked sequencing on the producer side and would introduce
memory-ordering correctness risk in a logging hot path that callers
implicitly trust to be ordered.

**Decision: do not adopt.** The ceiling is 5–6% even in the best case, and
the failure mode (silently lost or reordered entries under contention) is
exactly the kind of bug that is undetectable in unit tests and corrosive in
production. **Negative ROI**; closing.

## Where the real headroom is — and why we are *not* taking it now

The only allocation in the hot path that is large enough to matter is
`LogEntry` itself. Two viable directions:

| Approach | Potential gain | Cost / risk |
| -------- | -------------- | ----------- |
| Convert `LogEntry` to `record struct` | Removes ~1 Gen0/entry; could plausibly reduce wall time 30–50% | `Channel<LogEntry>` will box on enqueue unless the channel and every sink switch to a value-aware envelope; reference-typed payload fields (`Scopes`, `Properties`) are unaffected |
| `LogEntry` object pool | Same order of magnitude | Requires every `ILogSink` implementation to honor a "do not retain after `Emit` returns" contract — an externally observable, breaking convention |

Both options are **public-contract / SemVer-relevant** changes and do not
belong in a perf-cleanup pass. They should land as a deliberate proposal
with:

1. An RFC describing the new sink contract and migration path
2. A benchmark target (e.g. ">25% wall-time reduction on the formatting
   suite at N=100/200, no regression on the wait suite") agreed up front
3. Updated docs and sample sinks

Until then, the baseline above stands.

## How to reproduce

From repo root, on the pinned commit:

```sh
dotnet run -c Release --project benchmarks/PicoLog.Benchmarks -- main
```

See `benchmarks/PicoLog.Benchmarks/README.md` for the full suite layout.
