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
/// <para><b>The race self-heals, and collection is not the only way it goes stale.</b>
/// <see cref="LastRace"/> reports -1 once the tableau it came from is gone. A weak reference alone
/// is NOT sufficient for that: <c>CharacterTableauTextureProvider.Clear</c> calls
/// <c>CharacterTableau.OnFinalize</c>, which nulls every AgentVisuals the tableau owns, but the
/// provider keeps holding the managed object, so the weak target still resolves for as long as it
/// takes a GC to run. Marking a finalized tableau dirty redraws nothing, so the tuner would report
/// success while editing a race nobody is looking at. <see cref="ClearIf"/> is called from a
/// postfix on <c>OnFinalize</c> to close that window.</para>
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

    /// <summary>
    /// Drops the handle if it points at <paramref name="tableau"/>. Called when a tableau is
    /// finalized. Conditional because tableaux are torn down out of order: clearing unconditionally
    /// would let a closing encyclopedia page blank the handle to the inventory panel still on screen.
    /// </summary>
    public static void ClearIf(CharacterTableau tableau)
    {
        if (tableau == null)
            return;

        if (Slot.TryGetTarget(out var current) && ReferenceEquals(current, tableau))
            Clear();
    }

    /// <summary>Drops the handle. Used by tests to isolate cases from one another.</summary>
    public static void Clear()
    {
        Slot.SetTarget(null);
        _lastRace = -1;
    }
}
