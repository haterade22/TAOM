using System;
using System.Collections.Generic;
using TaleWorlds.Localization;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.LoadTimeStamps;

namespace TAOM;

/// <summary>
/// Applies one Harmony patch category at a time so a category whose target no longer resolves
/// costs only that category. Harmony's PatchCategory has no catch: the first class whose target is
/// missing throws a HarmonyException out of the caller, which in OnSubModuleLoad fails the module
/// load and in OnGameInitializationFinished skips every later category of the batch. Harmony does
/// not roll back, so the failing category's earlier classes stay patched. A class whose attributes
/// cannot be read (one naming a type the engine no longer has) would fail Harmony's assembly-wide
/// category index and so every category; PatchCategoryIndex skips that class instead, and
/// RecordSkippedClasses reports it here. Each failure or skipped class is logged at Error with its
/// full cause and remembered until the phase summary is taken.
/// The apply delegate keeps HarmonyLib out of this class, and its only engine type is the
/// TextObject it builds (never rendered here), so it is unit-testable.
/// Every apply is timed, failed or not: EndPhase logs the phase's always-on total, and
/// WriteHeldCategoryLines writes the per-category lines later, when the toggle that decides them
/// can be read (docs/features/load-time-stamps.md).
/// </summary>
internal sealed class PatchCategoryApplier
{
    private readonly Action<string> _apply;
    private readonly IModLogger _logger;
    private readonly List<string> _failed = new();
    private readonly IStampClock _clock;
    private readonly List<CategoryTiming> _phase = new();
    private readonly List<(string Phase, CategoryTiming Timing)> _held = new();

    internal PatchCategoryApplier(Action<string> apply, IModLogger logger)
        : this(apply, logger, StopwatchStampClock.Instance) { }

    internal PatchCategoryApplier(Action<string> apply, IModLogger logger, IStampClock clock)
    {
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Applies the category; on a throw, logs it, records it and returns false. True does not
    /// cover a class the index skipped (RecordSkippedClasses reports those), so a category that
    /// lost a class that way still returns true.
    /// </summary>
    internal bool TryApply(string category)
    {
        var start = _clock.Now;
        var ok = false;
        try
        {
            _apply(category);
            ok = true;
            return true;
        }
        catch (Exception ex)
        {
            _failed.Add(category);
            _logger.LogError(
                $"[PatchApply] {category} FAILED (Harmony stops a category at its first failing class): {ex}");
            return false;
        }
        finally
        {
            _phase.Add(new CategoryTiming(category, _clock.Now - start, ok));
        }
    }

    /// <summary>Logs the phase's total line (always) and keeps its per-category records for
    /// WriteHeldCategoryLines, because the toggle that decides them cannot be read in OnSubModuleLoad.</summary>
    internal void EndPhase(string phase)
    {
        long totalTicks = 0;
        var failed = 0;
        CategoryTiming? slowest = null;
        foreach (var timing in _phase)
        {
            totalTicks += timing.Ticks;
            if (!timing.Ok) failed++;
            if (slowest == null || timing.Ticks > slowest.Ticks) slowest = timing;
            _held.Add((phase, timing));
        }

        var frequency = _clock.Frequency;
        _logger.LogInfo(LoadTimeStampLines.PatchPhaseTotal(
            phase,
            _phase.Count,
            failed,
            LoadTimeStampLines.ToMs(totalTicks, frequency),
            slowest == null ? 0 : LoadTimeStampLines.ToMs(slowest.Ticks, frequency),
            slowest?.Category));
        _phase.Clear();
    }

    /// <summary>Writes every held per-category line when enabled, in apply order, then drops them.</summary>
    internal void WriteHeldCategoryLines(bool enabled)
    {
        if (enabled)
        {
            var frequency = _clock.Frequency;
            foreach (var (phase, timing) in _held)
                _logger.LogInfo(LoadTimeStampLines.PatchCategoryLine(phase, timing.Category, LoadTimeStampLines.ToMs(timing.Ticks, frequency), timing.Ok));
        }

        _held.Clear();
    }

    /// <summary>
    /// Logs each class the category index skipped because its attributes could not be read, and
    /// records it by name for the next phase summary.
    /// </summary>
    internal void RecordSkippedClasses(IEnumerable<KeyValuePair<Type, Exception>> skipped)
    {
        foreach (var entry in skipped)
        {
            var name = entry.Key.FullName ?? entry.Key.Name;
            _failed.Add(name);
            _logger.LogError(
                $"[PatchApply] {name} SKIPPED (its attributes cannot be read, so its patches are off; every other class still applies): {entry.Value}");
        }
    }

    /// <summary>
    /// One player-facing line naming every category that failed since the last call, or null when
    /// none did. Clears the list, so each phase reports only its own failures. A localized
    /// TextObject built here but rendered by the caller; the category ids stay literal.
    /// </summary>
    internal TextObject? TakeFailureSummary(TextObject phase)
    {
        if (_failed.Count == 0) return null;

        var summary = new TextObject("{=taom_patch_apply_failed}TAOM: patch groups failed to apply during {PHASE}: {GROUPS}. Some fixes in those groups are off this session; the TAOM log names the cause.")
            .SetTextVariable("PHASE", phase)
            .SetTextVariable("GROUPS", string.Join(", ", _failed));
        _failed.Clear();
        return summary;
    }

    private sealed class CategoryTiming
    {
        internal CategoryTiming(string category, long ticks, bool ok)
        {
            Category = category;
            Ticks = ticks;
            Ok = ok;
        }

        internal string Category { get; }
        internal long Ticks { get; }
        internal bool Ok { get; }
    }
}
