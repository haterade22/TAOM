using System.Collections.Generic;

namespace TAOM.Features.DreadAura.Hooks;

/// <summary>
/// Decides which dread sources pulse on a given frame.
///
/// Round-robin, so a field with many wraiths spreads its proximity queries across frames rather
/// than spiking one: each <c>Mission.GetNearbyEnemyAgents</c> call takes a process-wide static lock
/// inside the engine, shared with vanilla's own morale logic.
///
/// Touches no engine type, so the rotation, the budget, the interval and the catch-up bound are
/// unit-testable. It stamps <c>LastPulseTime</c> itself, which is what stops one source being
/// selected twice in a frame and is what makes each source's drain integrate against its OWN
/// elapsed time.
/// </summary>
public sealed class DreadPulseScheduler
{
    /// <summary>
    /// Ceiling on how much elapsed time one pulse may carry, in seconds, or one pulse interval if
    /// that is longer.
    ///
    /// Without this, ANY window in which the aura is skipped freezes <c>LastPulseTime</c> while the
    /// mission clock keeps running, and the whole window is then delivered as a single pulse.
    /// Toggling the MCM off and back on mid-battle would drain the entire off-window at once and
    /// instantly rout every enemy in radius; a long stall or alt-tab would do the same. Genuine
    /// frame hitches still get exact catch-up, because they are far below this bound.
    /// </summary>
    private const float MaxCatchUpSeconds = 1f;

    private int _cursor;

    public void Reset() => _cursor = 0;

    /// <summary>
    /// Appends every source due to pulse, newest cursor position first, up to
    /// <paramref name="budget"/> entries, and stamps each selected source as pulsed at
    /// <paramref name="now"/>. Reported elapsed time is bounded by
    /// <see cref="MaxCatchUpSeconds"/>.
    /// </summary>
    public void SelectDue(
        IReadOnlyList<DreadSourceTracker.DreadSource> sources,
        float now,
        float interval,
        int budget,
        List<DuePulse> due)
    {
        due.Clear();

        if (sources == null || sources.Count == 0)
            return;

        // Positive requirement, so a NaN interval selects nothing rather than everything.
        if (!(interval >= 0f))
            return;

        var ceiling = interval > MaxCatchUpSeconds ? interval : MaxCatchUpSeconds;

        for (var examined = 0; examined < sources.Count && due.Count < budget; examined++)
        {
            _cursor = (_cursor + 1) % sources.Count;
            var source = sources[_cursor];
            if (source == null)
                continue;

            var elapsed = now - source.LastPulseTime;
            if (!(elapsed >= interval))
                continue;

            source.LastPulseTime = now;
            due.Add(new DuePulse(_cursor, elapsed > ceiling ? ceiling : elapsed));
        }
    }

    public readonly struct DuePulse
    {
        public DuePulse(int index, float elapsed)
        {
            Index = index;
            Elapsed = elapsed;
        }

        public int Index { get; }

        /// <summary>Seconds since THIS source last pulsed, bounded by the catch-up ceiling.</summary>
        public float Elapsed { get; }
    }
}
