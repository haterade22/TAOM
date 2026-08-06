using System;
using System.Collections.Generic;

namespace TAOM.Features.HeroRace.Diagnostics;

/// <summary>When a character's tableau is worth dumping a render census for.</summary>
public enum TableauResidencyVerdict
{
    /// <summary>Nothing to report — still loading, or this character already reported.</summary>
    None = 0,

    /// <summary>
    /// The visual finished loading after genuinely waiting on resources. Its meshes and materials
    /// are attached, so a census taken now describes what will be drawn.
    /// </summary>
    Ready,

    /// <summary>
    /// The loading counters never cleared within the threshold. Census anyway — it will be partial,
    /// and the tableau is most likely BLANK rather than black (vanilla never runs the branch that
    /// makes a visual visible until both counters clear).
    /// </summary>
    Timeout,

    /// <summary>
    /// Capacity reached; this and later new characters are not tracked. Emitted once so that a
    /// missing character in the log is never silently mistaken for "nothing was wrong".
    /// </summary>
    CapacityReached,
}

/// <summary>One verdict plus the tick count that produced it.</summary>
public readonly struct TableauResidencyResult
{
    public TableauResidencyResult(TableauResidencyVerdict verdict, int ticks)
    {
        Verdict = verdict;
        Ticks = ticks;
    }

    public TableauResidencyVerdict Verdict { get; }

    /// <summary>Observations made for this character before the verdict, inclusive.</summary>
    public int Ticks { get; }
}

/// <summary>
/// Decides WHEN to dump a render census for a character tableau, exactly once (issue #389).
///
/// It is only a trigger, not a diagnosis. An earlier version of this class reported
/// "counter stuck ⇒ black silhouette", which an adversarial review refuted against the v1.4.7
/// decompile: vanilla only makes a visual visible once BOTH loading counters clear
/// (<c>CharacterTableau.OnTick</c>), and <c>RefreshCharacterTableau</c> hides the freshly-refreshed
/// buffer outright. A character whose counters never clear therefore renders BLANK, not black — so
/// for a bug whose symptom is "correct geometry, black pixels", the counters must already have
/// cleared. Residency cannot be the mechanism, and a verdict framed around it could only ever say
/// "fine". What the counters are still good for is telling us the precise frame at which the meshes
/// and materials exist and are worth reading.
/// </summary>
public sealed class TableauResidencyTracker
{
    /// <summary>Frames, not seconds — <c>OnTick</c> runs once per rendered frame, so this is ~3s at 60 Hz and ~1.25s at 144 Hz.</summary>
    public const int DefaultStuckThresholdTicks = 180;

    /// <summary>
    /// Browsing a troop tree walks hundreds of characters, and every screen binding
    /// <c>CharStringId</c> (party, inventory, clan, encyclopedia) consumes slots.
    /// </summary>
    public const int DefaultMaxTrackedCharacters = 64;

    private sealed class Entry
    {
        public int Ticks;
        public bool Reported;

        /// <summary>
        /// Guards the sentinel collision. <c>_agentVisualLoadingCounter</c> is <c>0</c> both BEFORE a
        /// refresh has ever armed it and AFTER loading completes — the two states share an encoding
        /// (`.claude/rules/harmony-patches.md`, "Static State Machines: Sentinel-Collision Check").
        /// Without this flag a tableau observed before its first refresh reports Ready having
        /// measured nothing, and burns the character's single verdict.
        /// </summary>
        public bool EverObservedLoading;
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private bool _capacityReported;

    public TableauResidencyTracker(
        int stuckThresholdTicks = DefaultStuckThresholdTicks,
        int maxTrackedCharacters = DefaultMaxTrackedCharacters)
    {
        // Clamp rather than throw: constructed on a render path, and a diagnostic must not be the
        // thing that takes the game down.
        StuckThresholdTicks = stuckThresholdTicks > 0 ? stuckThresholdTicks : DefaultStuckThresholdTicks;
        MaxTrackedCharacters = maxTrackedCharacters > 0 ? maxTrackedCharacters : DefaultMaxTrackedCharacters;
    }

    public int StuckThresholdTicks { get; }

    public int MaxTrackedCharacters { get; }

    public int TrackedCount
    {
        get { lock (_gate) { return _entries.Count; } }
    }

    /// <summary>
    /// Records one tick and returns a verdict the first time one is reachable for
    /// <paramref name="characterKey"/>.
    ///
    /// BOTH counters must clear, because vanilla gates the visibility swap on
    /// <c>_mountVisualLoadingCounter == 0 &amp;&amp; _agentVisualLoadingCounter == 0</c>. Watching only
    /// the agent counter would call a mounted troop Ready while its mount was still streaming.
    /// </summary>
    public TableauResidencyResult Observe(string characterKey, int agentVisualLoadingCounter, int mountVisualLoadingCounter)
    {
        if (string.IsNullOrWhiteSpace(characterKey)) return default;

        lock (_gate)
        {
            if (!_entries.TryGetValue(characterKey, out var entry))
            {
                // Capacity rejects NEW characters only — dropping one already in flight would lose
                // the single observation this investigation depends on. Announce it once: silence
                // for a character must never be mistakable for a clean result.
                if (_entries.Count >= MaxTrackedCharacters)
                {
                    if (_capacityReported) return default;
                    _capacityReported = true;
                    return new TableauResidencyResult(TableauResidencyVerdict.CapacityReached, 0);
                }

                entry = new Entry();
                _entries[characterKey] = entry;
            }

            if (entry.Reported) return default;

            entry.Ticks++;

            bool stillLoading = agentVisualLoadingCounter > 0 || mountVisualLoadingCounter > 0;
            if (stillLoading)
            {
                entry.EverObservedLoading = true;
                if (entry.Ticks >= StuckThresholdTicks)
                {
                    entry.Reported = true;
                    return new TableauResidencyResult(TableauResidencyVerdict.Timeout, entry.Ticks);
                }
                return default;
            }

            // Counters clear. Only meaningful if we actually saw them non-clear first; otherwise the
            // refresh has not happened yet and there is nothing attached to census.
            if (!entry.EverObservedLoading) return default;

            entry.Reported = true;
            return new TableauResidencyResult(TableauResidencyVerdict.Ready, entry.Ticks);
        }
    }

    /// <summary>Drops a character's state so it can report again after a teardown/rebuild.</summary>
    public void Forget(string characterKey)
    {
        if (string.IsNullOrWhiteSpace(characterKey)) return;
        lock (_gate) { _entries.Remove(characterKey); }
    }
}
