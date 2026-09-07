using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Patch85 — moves the enlisted battle-end detach OUT of the MapEventEnded dispatch and into the one
/// statement of headroom vanilla leaves for it. Issue #557.
///
/// THE PROBLEM THIS SOLVES. Campaign-event listeners are LIFO:
/// <c>MbEvent{T}.AddNonSerializedListener</c> (installed v1.4.8, :24-30) head-inserts and
/// <c>Invoke</c> :32-35 walks from the head. TAOM registers its behaviours in
/// <c>SubModule.OnGameStart</c> after SandBox has registered its own, so TAOM's
/// <c>EnlistmentBattleBehavior.OnMapEventEnded</c> runs BEFORE vanilla's
/// <c>SiegeAftermathCampaignBehavior.OnMapEventEnded</c>. When it detached there, clearing
/// <c>AttachedTo</c> made <c>MobileParty.SetAttachedToInternal</c> :1780-1783 drop the main party out
/// of <c>MapEventSide._battleParties</c>, so vanilla's <c>IsMainPartyAmongParties()</c> was false, its
/// assignment block was skipped, and its own aftermath menu then dereferenced a null
/// <c>_besiegerParty</c>. Patch84 guards that menu; this removes the reason it fires.
///
/// WHY THIS EXACT TARGET. <c>PlayerEncounter.Finish</c> runs two consecutive statements:
/// <c>FinalizeBattle()</c> :1071, inside which <c>MapEvent.FinalizeEvent</c> dispatches
/// <c>MapEventEnded</c> to every listener, and then <c>FinishEncounterInternal()</c> :1072, which
/// grants the post-defeat escape (<c>TeleportPartyToOutSideOfEncounterRadius</c> +
/// <c>SetDoNotAttackMainParty(2)</c>) ONLY when <c>MainParty.AttachedTo == null</c>. A postfix on
/// <c>FinalizeBattle</c> therefore lands after every vanilla listener has read the party list and
/// before the escape is decided. That window is one statement wide and it is the only place both
/// constraints hold at once.
///
/// The later <c>if (InsideSettlement &amp;&amp; MainParty.AttachedTo == null ...) LeaveSettlement()</c>
/// at :1082 also still sees the detach, since it runs after both.
///
/// FAIL-OPEN, AND WITH A STANDING BACKSTOP. A throw here is swallowed: the worst case is that the
/// player stays attached for a tick, and <c>EnlistmentReconciler</c>'s
/// <c>noBattleAnywhere &amp;&amp; IsInArmy</c> sweep already exists to catch a battle that resolved
/// without the normal edge. That is why this patch can be a thin postfix rather than something that
/// has to be transactionally correct.
///
/// ADR-002: no logic here. The service owns every gate, including the #551 live-map-event check.
/// </summary>
[HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.FinalizeBattle))]
[HarmonyPatchCategory("Patch85_EnlistedDetachDeferral")]
public static class Patch85_EnlistedDetachDeferral
{
    private static IModLogger _logger;
    private static Func<IServiceBattleService> _battle;
    private static Func<ICoopSessionProvider> _coop;

    /// <summary>
    /// Resolvers rather than instances: this runs in the standard patch batch, and resolving the
    /// service graph eagerly at that point would pull enlistment construction earlier than its own
    /// registration. The delegates are invoked on the first battle end, by which time the container
    /// is complete.
    /// </summary>
    internal static void Initialize(
        IModLogger logger,
        Func<IServiceBattleService> battle,
        Func<ICoopSessionProvider> coop)
    {
        _logger = logger;
        _battle = battle;
        _coop = coop;
    }

    [HarmonyPostfix]
    public static void Postfix()
    {
        try
        {
            // Co-op: only the authority mutates shared party state. Same gate
            // EnlistmentBattleBehavior.OnMapEventEnded applies to the path this defers from, so the
            // deferral cannot widen who is allowed to detach.
            if (_coop?.Invoke()?.IsAuthority == false)
                return;

            _battle?.Invoke()?.FlushArmyLeaveAfterBattle();
        }
        catch (Exception ex)
        {
            // Never propagate into PlayerEncounter.Finish. A missed detach is recoverable by the
            // reconciler; an exception here would take the player's battle-end path with it.
            try
            {
                _logger?.LogError(
                    $"[Enlistment] deferred army leave failed at FinalizeBattle, leaving it to the " +
                    $"reconciler's no-battle sweep: {ex}");
            }
            catch { /* never throw out of a postfix */ }
        }
    }

    /// <summary>Test seam. The bindings are process-global, so a test that rebinds must restore them.</summary>
    internal static void ResetForUnload()
    {
        _logger = null;
        _battle = null;
        _coop = null;
    }
}
