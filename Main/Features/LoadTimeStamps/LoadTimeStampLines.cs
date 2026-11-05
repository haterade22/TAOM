using System;

namespace TAOM.Features.LoadTimeStamps;

/// <summary>
/// Every load-time stamp line (docs/features/load-time-stamps.md "Log lines"), kept pure so the
/// tests pin each format literally. Milliseconds always print with two decimals and a dot,
/// whatever the player's culture. A line with key=value pairs holds only pairs from its first one on:
/// a total line says <c>scope=total</c>, never a bare "total", because the perf-runs log parser
/// (plan 029) reads a bare word after a value as part of that value.
/// </summary>
internal static class LoadTimeStampLines
{
    /// <summary>A campaign handler gets its own [Lifecycle] line when its summed time is at least this.</summary>
    internal const double HandlerThresholdMs = 10.0;

    private const string None = "none";

    internal static double ToMs(long ticks, long frequency) => frequency <= 0 ? 0.0 : ticks * 1000.0 / frequency;

    // H1: the stage A configuration header, once per process.
    internal static string Ready() =>
        "[LoadStamps] ready: [PatchApply] phase totals are always written; per-category [PatchApply] and per-hook [LoadPhase] lines follow \"Enable Load-Time Stamps\" (Battle Load Diagnostics page, default off): the per-hook lines read it at each game start and game initialization, the per-category lines once, at the first game initialization after launch";

    // H2: the toggle's value: the first read, a change, and the first readable read after an H3.
    internal static string Detail(bool on) => on
        ? "[LoadStamps] detail on: the per-category, per-hook and per-handler load-time lines are written"
        : "[LoadStamps] detail off: only the always-on load-time totals are written; turn on \"Enable Load-Time Stamps\" for the per-category, per-hook and per-handler lines";

    // H3: the toggle could not be read: the first read, or the first failed read after a readable one.
    internal static string DetailUnreadable(Exception exception) =>
        $"[LoadStamps] detail off: \"Enable Load-Time Stamps\" could not be read ({exception.GetType().Name}: {exception.Message}); only the always-on load-time totals are written";

    // P1: one patch category's apply time, written at game initialization (or the first mission).
    internal static string PatchCategoryLine(string phase, string category, double ms, bool ok) =>
        FormattableString.Invariant($"[PatchApply] phase={phase} category={category} ms={ms:0.00} result={(ok ? "ok" : "failed")}");

    // P2: one apply phase's total, always written at the phase end.
    internal static string PatchPhaseTotal(string phase, int categories, int failed, double ms, double maxMs, string? maxCategory) =>
        FormattableString.Invariant(
            $"[PatchApply] phase={phase} scope=total categories={categories} failed={failed} ms={ms:0.00} max_ms={maxMs:0.00} max_category={maxCategory ?? None}");

    // L1: one step of a TAOM hook, timed from the end of the previous step's line (the start, for the first).
    internal static string HookStep(string hook, string step, double ms) =>
        FormattableString.Invariant($"[LoadPhase] hook={hook} step={step} ms={ms:0.00}");

    // L2: a TAOM hook's wall-clock total from its start (its lines' writes included) and step count.
    internal static string HookTotal(string hook, string? game, double ms, int steps) =>
        FormattableString.Invariant($"[LoadPhase] hook={hook} game={game ?? None} scope=total ms={ms:0.00} steps={steps}");

    // The stage B configuration header, once per process.
    internal static string LoadXmlReady() =>
        "[LoadXml] ready: one line per MBObjectManager.LoadXML call and a summary at every game initialization, always written";

    // X1: one MBObjectManager.LoadXML call.
    internal static string LoadXml(string id, int files, double ms, int xslt, double? mergeMs, double? objectsMs, string result) =>
        FormattableString.Invariant(
            $"[LoadXml] id={id} files={files} ms={ms:0.00} xslt={xslt} merge_ms={OrNone(mergeMs)} objects_ms={OrNone(objectsMs)} result={result}");

    // X2: every LoadXML call since the last summary, at each game initialization.
    internal static string LoadXmlSummary(string? game, int calls, int files, int xslt, double ms,
                                          double mergeMs, double objectsMs, double maxMs, string? maxId, int failed) =>
        FormattableString.Invariant(
            $"[LoadXml] summary game={game ?? None} calls={calls} files={files} xslt={xslt} ms={ms:0.00} merge_ms={mergeMs:0.00} objects_ms={objectsMs:0.00} max_ms={maxMs:0.00} max_id={maxId ?? None} failed={failed}");

    // The stamp's own fault, once per process.
    internal static string LoadXmlFault(Exception exception) =>
        $"[LoadXml] stamp fault, some [LoadXml] lines may be missing this session: {exception.GetType().Name}: {exception.Message}";

    // The stage C configuration header, once per process.
    internal static string LifecycleReady(string? bindingProblem) => bindingProblem == null
        ? FormattableString.Invariant(
            $"[Lifecycle] ready: listener binding ok; a dispatch line is always written for each new-game, game-loaded and session-start dispatch; with \"Enable Load-Time Stamps\" on, every handler of them is timed too, with a line for each at or over {HandlerThresholdMs:0.00} ms")
        : $"[Lifecycle] ready: listener binding missing ({bindingProblem}); only a dispatch line for each new-game, game-loaded and session-start dispatch is written, whatever \"Enable Load-Time Stamps\" says";

    // C1: one campaign handler of one event, at or over the threshold.
    internal static string Handler(string evt, string handler, string assembly, int calls, double ms, double maxMs, int maxIndex) =>
        FormattableString.Invariant(
            $"[Lifecycle] event={evt} handler={handler} asm={assembly} calls={calls} ms={ms:0.00} max_ms={maxMs:0.00} max_index={(maxIndex < 0 ? None : maxIndex.ToString(System.Globalization.CultureInfo.InvariantCulture))}");

    // C2: one event's total.
    internal static string EventTotal(string evt, int listeners, int taomListeners, double ms, double taomMs, double otherMs,
                                      int overThreshold, double maxMs, string? maxHandler) =>
        FormattableString.Invariant(
            $"[Lifecycle] event={evt} scope=total listeners={listeners} taom_listeners={taomListeners} ms={ms:0.00} taom_ms={taomMs:0.00} other_ms={otherMs:0.00} over_threshold={overThreshold} max_ms={maxMs:0.00} max_handler={maxHandler ?? None}");

    // C3: one dispatcher call.
    internal static string Dispatch(string dispatch, double ms, double? listenersMs, string result) =>
        FormattableString.Invariant($"[Lifecycle] dispatch={dispatch} ms={ms:0.00} listeners_ms={OrNone(listenersMs)} result={result}");

    // C4: per-handler timing switched itself off, once per process.
    internal static string LifecycleOff(string problem) =>
        $"[Lifecycle] per-handler timing off for this session: {problem}; dispatch totals are still written";

    // C5: the stamp's own fault (a clock or logger failure), once per process.
    internal static string LifecycleFault(Exception exception) =>
        $"[Lifecycle] stamp fault, some [Lifecycle] lines may be missing this session: {exception.GetType().Name}: {exception.Message}";

    // C6: a listener could not be put back, once per process: its record keeps the timing wrapper.
    internal static string LifecycleRestoreFault(Exception exception) =>
        $"[Lifecycle] restore fault, a campaign handler may keep its timing wrapper this session (it still runs once per call): {exception.GetType().Name}: {exception.Message}";

    private static string OrNone(double? ms) =>
        ms.HasValue ? FormattableString.Invariant($"{ms.Value:0.00}") : None;
}
