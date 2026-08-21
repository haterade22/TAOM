using System;
using TaleWorlds.MountAndBlade.View.Tableaus;

namespace TAOM.Features.HeroRace;

/// <summary>
/// Weak handle on the character tableau that most recently refreshed, plus the race it was showing.
///
/// <para>Exists solely for the in-game offset tuner: to see a nudge you need the tableau currently
/// on screen, and nothing in the view layer hands one out. Every path here is null-tolerant because
/// a tableau is torn down whenever its screen closes, which is routine, not exceptional.</para>
///
/// <para>The reference is weak on purpose. A strong static reference to a tableau would pin its
/// scene, agent visuals and skeletons for the life of the process, which is the exact shape of leak
/// the tournament-exit work spent two rounds chasing.</para>
///
/// <para><b>The race self-heals.</b> <see cref="LastRace"/> reports -1 once the tableau it came
/// from has been collected, rather than continuing to name a race nobody is looking at. Without
/// that, the tuner "." shorthand would keep resolving to a stale-but-VALID race after the screen
/// closed, and silently edit the offsets of a race the player cannot see.</para>
/// </summary>
public static class LiveTableauRef
{
    private static readonly WeakReference<CharacterTableau> Slot = new WeakReference<CharacterTableau>(null);
    private static int _lastRace = -1;

    public static void Set(CharacterTableau tableau, int race)
    {
        if (tableau == null)
            return;

        // SetTarget on a reused WeakReference rather than allocating a new one: this runs on every
        // tableau refresh, and WeakReference is finalizable, so a fresh instance per refresh would
        // add finalizer-queue churn for no benefit.
        Slot.SetTarget(tableau);
        _lastRace = race;
    }

    /// <summary>
    /// The race id last previewed, or -1 when no tableau has refreshed yet or the one that did has
    /// been collected.
    /// </summary>
    public static int LastRace => TryGet(out _) ? _lastRace : -1;

    public static bool TryGet(out CharacterTableau tableau)
    {
        if (Slot.TryGetTarget(out tableau) && tableau != null)
            return true;

        tableau = null;
        return false;
    }

    /// <summary>Drops the handle. Used by tests to isolate cases from one another.</summary>
    public static void Clear()
    {
        Slot.SetTarget(null);
        _lastRace = -1;
    }
}
