# Perf method

How to make and judge a performance change. The measuring tool is `tools/perf_runs.py` and the
`[MissionPerf]` heartbeat ([mission-perf-heartbeat.md](../features/mission-perf-heartbeat.md), "Reading an A/B").
"It felt faster" is not a result.

## 1. Pick the move: cheapest first

Try these in order; when an earlier one meets the target, stop.

1. Do not do it. Remove work whose result nothing reads.
2. Do not do it again. Cache it, and key the cache on something stable (never `Agent.Index`).
3. Do less. Fewer agents, fewer ticks, a smaller radius.
4. Do it later. Move it off the mission's first frames or the load path.
5. Do it off the hot frame. Spread it across ticks or run it on a timer.
6. Do it concurrently. Engine calls stay on the main thread; a write back goes through `DeferredCallbackQueue.RunOrDefer` ([csharp-architecture.md](../../.claude/rules/csharp-architecture.md)).
7. Do it cheaper. A cheaper algorithm or allocation-free path, last.

## 2. Freeze the harness

Fix one scene, one save or custom battle, one build type and texture quality. The harness prints a
count of the work it did (agents spawned, ticks run, items processed) next to the metric. Do not
change it between attempts; `compare` refuses groups whose `[PerfContext]` build or texture quality
differ.

Before each timing run, list the busiest processes (`Get-Process | Sort CPU -desc | select -first 8`) and end or wait out
anything that is not the game. A stray `python` started through the Store alias by a `python3` call ran 26 hours on one
core and made the numbers of a whole day of yotthani's runs provisional, while the structural findings stayed valid (yotthani, `yotthani/MithrilForge` `docs/perf-audit/profiling.md`@ea7a7bfb
lines 72-74, 83-84 and 86-87; [lesson](../reviews/lessons/build-tooling-workflow.md), "A stray process that eats a core"). When kernel
(ETW) sampling loses events, the same file's fallback is a Very Sleepy user-mode capture with module load addresses saved,
mapped to Ghidra function ranges (`python tools/native_decompile.py` is TAOM's side of that lookup).

## 3. The metric

The median of at least 5 runs per side, A and B alternated (A, B, A, B), never one run. The tool
also flags rows from mixed scenes, a metric with fewer than 5 values in a group, and a group that
holds two values of a shown setting (`settings` line: shadow type, filtering, quality) as inconclusive.
Capture with:

```
python tools/perf_runs.py <logs...>
python tools/perf_runs.py compare --a <logs without the change...> --b <logs with it...> --scene <scene id>
```

A gap inside the run-to-run range is no measurable difference.

Measure TAOM's own run-to-run spread before trusting a window that is fixed in time: two identical runs of one scene, same
build. yotthani's two identical runs of 01.10.2026 were 25 % apart in a fixed window because the clash came at different times, and
4 % apart when compared per fight phase (ms per frame, bucketed by active agents: 900 to 1000, 700 to 899, 500 to 699, under
500; a phase counts when every run holds at least 10 s of it) (`docs/perf-audit/scenarios/s1.md`@ea7a7bfb line 30, one scenario,
game 1.5.3). `tools/perf_runs.py` cuts its steady windows from `t >= 30 s` with `active > 0` and does not bucket by agent
count (its header describes no phase bucket). TAOM's own spread is not recorded in this repo's docs.

## 4. Prove the work ran

Before trusting a number, confirm the work happened inside the timed region: the mission ticked
(steady windows exist, `active` above 0, the heartbeat reached `t=+30s`), and the harness's work
count matches the baseline. A timing for a path that never ran is the usual false win. Check that
the faster path gives the same result against something the change did not touch (a test, the
baseline's output), never against a second copy of the changed code.

## 5. Stop rule

Pair a target with a floor on attempts, for example "at least 15% lower median `avgMs` and at least
5 attempts", so a lucky first win cannot end the run. Do not relax the target to meet it. Stop early
only when the remaining ideas are marginal, and say so.

## 6. One row per attempt

| id | hypothesis | change | before | after | verdict |
|---|---|---|---|---|---|

The hypothesis names a mechanism ("the AI tick scans every agent past 15 m"), not "try caching".
Keep the change only when the metric moves past noise and the tests stay green. A change that only
"might help" is reverted in full.

## 7. Name the limiter

Say why the number moved or did not: main-thread bound (`mainMs` near `avgMs`), render bound
(`renderMs` near `avgMs`), the frame limiter, GC (`gc2` per minute). A result with no nameable
limiter is reported as inconclusive, with the gap named.

Adapted from backnotprop/pstack (MIT); the independent-check line in section 4 from morluto/rea (MIT).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/mission-perf-heartbeat.md](../features/mission-perf-heartbeat.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
