using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TAOM.Core.Logging;

namespace TAOM.Features.MapEventGuard.Hooks;

/// <summary>
/// Patch82 — restores the engine's own invariant that a <c>MapEvent</c> with a
/// <c>BattleObserver</c> also has a <c>TroopUpgradeTracker</c>, before the simulation reads it.
///
/// THE INVARIANT AND WHY IT IS NEVER RE-CHECKED (verified against installed v1.4.8):
/// <c>MapEventSide.AllocateTroops</c> :552 and <c>AllocateTroop</c> :590 both call
/// <c>_mapEvent.TroopUpgradeTracker.AddTrackedTroop(...)</c> with no null check, gated only on
/// <c>BattleObserver != null</c>; <c>ApplySimulatedHitRewardToSelectedTroop</c> :1050/:1056 does the
/// same behind an early <c>if (BattleObserver == null) return;</c> at :1040. Four unguarded
/// dereferences across three methods, all leaning on the pairing holding.
///
/// IT DOES NOT ALWAYS HOLD. The tracker is nulled outright by
/// <c>MapEvent.RemoveInvolvedPartyInternal</c> :855-858 whenever the removed party is
/// <c>PartyBase.MainParty</c>, and it is only rebuilt if the main party rejoins
/// (<c>AddInvolvedPartyInternal</c> :636) or the save is reloaded (<c>OnAfterLoad</c> :530). The
/// observer has exactly one writer in the whole game, the <c>BattleSimulation</c> constructor, which
/// assigns it and only THEN indexes <c>SelectedTroops[(int)_mapEvent.PlayerSide]</c> — and
/// <c>BattleSideEnum.None</c> is <c>-1</c>, so a main party with no <c>MapEventSide</c> makes that
/// line throw <c>IndexOutOfRangeException</c> with the observer already attached. Its one clearer,
/// <c>PlayerEncounter.LeaveBattle</c> :1990, never runs on that path.
///
/// Removing the main party is also what puts the event back in reach of
/// <c>MapEventManager.Tick</c> :59, whose condition is
/// <c>IsRaid || _mapEvents[i] != MobileParty.MainParty.MapEvent</c> — so a non-raid event is skipped
/// exactly while it IS the player's, and becomes tickable again the moment he leaves it. One detach
/// therefore supplies both halves: the null tracker, and the tick that dereferences it.
///
/// Crash bundle 31942985 (issue #551) is that NRE, reported from a live game. TAOM's enlistment gate
/// is the fix for the path that got there; this is the floor under every other path, including ones
/// that do not exist yet.
///
/// CLEARING THE OBSERVER, NOT REBUILDING THE TRACKER, is the repair. The observer is a
/// <c>BattleSimulation</c> whose constructor threw, so <c>PlayerEncounter.Current.BattleSimulation</c>
/// was never assigned and nothing else holds it: there is no scoreboard left to feed. Rebuilding the
/// tracker would instead invent state for a battle the player is provably not in — the tracker is
/// null precisely because the main party was removed.
/// </summary>
[HarmonyPatch(typeof(MapEvent), nameof(MapEvent.SimulateBattleSetup))]
[HarmonyPatchCategory("Patch82_MapEventObserverInvariant")]
public static class Patch82_MapEventObserverInvariant
{
    private static IModLogger _logger;

    private static PropertyInfo _battleObserver;

    /// <summary>
    /// False when the binding failed. The prefix then does nothing at all, so an engine rename
    /// degrades to "the guard is not installed" rather than throwing inside every simulated battle
    /// in the world.
    /// </summary>
    internal static bool IsReady { get; private set; }

    /// <summary>
    /// Resolved once, never inside the prefix: <c>SimulateBattleSetup</c> runs for every live map
    /// event on its own simulation timer, which is many times a second at accelerated campaign
    /// speed.
    /// </summary>
    internal static void Initialize(IModLogger logger)
    {
        _logger = logger;

        // Internal on MapEvent, so AccessTools rather than a direct reference. The getter is what
        // the invariant is read through and the setter is the repair; both must resolve or the
        // guard is not installable.
        _battleObserver = AccessTools.Property(typeof(MapEvent), "BattleObserver");

        IsReady = _battleObserver != null
                  && _battleObserver.GetGetMethod(nonPublic: true) != null
                  && _battleObserver.GetSetMethod(nonPublic: true) != null;

        if (!IsReady)
        {
            _logger?.LogWarning(
                "[MapEventGuard] MapEvent.BattleObserver did not resolve — Patch82 is inert and the " +
                "AllocateTroops null-tracker crash (#551) is unguarded on this engine build.");
        }
    }

    [HarmonyPrefix]
    public static void Prefix(MapEvent __instance)
    {
        if (!IsReady || __instance == null)
            return;

        try
        {
            // Cheapest term first: the tracker is non-null for every ordinary AI battle, which is
            // essentially all of them, so this returns immediately in the common case.
            if (__instance.TroopUpgradeTracker != null)
                return;

            if (_battleObserver.GetValue(__instance, null) == null)
                return;

            _battleObserver.SetValue(__instance, null, null);

            // No per-event throttle needed: the repair makes its own condition false, so this fires
            // once unless something attaches a second dangling observer, which is worth hearing.
            _logger?.LogWarning(
                "[MapEventGuard] cleared a dangling BattleObserver on a map event whose " +
                "TroopUpgradeTracker was null — MapEventSide.AllocateTroops would have thrown (#551). " +
                "The main party was removed from this event while its battle UI stayed attached.");
        }
        catch (Exception ex)
        {
            // A guard that throws is worse than a guard that is absent: this runs inside the
            // campaign tick, ahead of the vanilla simulation for every map event in the world.
            _logger?.LogError($"[MapEventGuard] observer-invariant check failed, deferring to vanilla: {ex}");
        }
    }

    /// <summary>Test seam. The binding is process-global, so a test that rebinds must restore it.</summary>
    internal static void ResetForUnload()
    {
        _logger = null;
        _battleObserver = null;
        IsReady = false;
    }
}
