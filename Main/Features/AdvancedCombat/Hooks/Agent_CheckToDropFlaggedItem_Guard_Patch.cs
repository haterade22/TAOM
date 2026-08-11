using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat.Hooks;

/// <summary>
/// Prevents — and as a backstop still swallows — a vanilla <see cref="NullReferenceException"/> in
/// <c>Agent.CheckToDropFlaggedItem()</c>.
///
/// WHY: TAOM's synthetic creature bites (warg + spider + elephant/mûmakil, via
/// <c>CustomAttacksUtils.TakeDamage</c> → <c>Mission.RegisterBlow</c> → <c>Agent.HandleBlow</c> →
/// <c>Mission.OnAgentHit</c>) call <c>affectedAgent.CheckToDropFlaggedItem()</c> on the victim as
/// <c>OnAgentHit</c>'s LAST statement. The method's only guard tests the wielded INDEX, never
/// <c>Equipment</c> nor the resolved <c>Item</c>, so it NREs at Agent.cs:3604 on either a null
/// <c>Equipment</c> or a null <c>Item</c> in a wielded slot.
///
/// SCOPE — the engine calls this from THREE sites, not just the bite path (v1.4.8):
/// <c>Agent.OnMount</c> (:12142) and <c>Agent.OnDismount</c> (:12167), both inside
/// <c>if (!GameNetwork.IsClientOrReplay)</c>, and <c>Mission.OnAgentHit</c> (:57869). So this
/// Prefix runs on every mount, every dismount and every agent-hit in every battle — not only on
/// TAOM creature attacks. Keep it allocation-free. Note `OnMount` calls it BEFORE its own
/// <c>if (HasBeenBuilt)</c> block, which is direct engine evidence that a not-yet-built agent
/// reaching this method is a state vanilla itself expects.
///
/// Observed twice, and NOT the same shape both times:
///   • 2026-06-17 (crash report, warg-vs-warg) — read at the time as a non-vanilla MOUNT victim
///     whose <c>Equipment[wieldedIndex].Item</c> was null.
///   • 2026-08-10 (live debugger, warg battle) — victim was NOT a mount: <c>IsHuman=true</c>,
///     <c>IsMount=false</c>, <c>State=Active</c>, <c>Health=13</c>, no rider, no mount, flags
///     carrying <c>CanWieldWeapon</c> — and <c>Character == null</c>, i.e. an agent that is not
///     yet fully built. <c>Equipment</c> is assigned exactly once, by
///     <c>InitializeMissionEquipment</c> from <c>Agent.Build</c> (Agent.cs:5174), and
///     <c>Agent.Clear</c> (:5194) does NOT null it — so a null <c>Equipment</c> is a SPAWN-time
///     window, not a teardown one, and the INDEXER is the throw rather than <c>.Item</c>.
///     Worth knowing for anyone tempted to gate on <c>HasBeenBuilt</c> instead: <c>Build</c> sets
///     <c>HasBeenBuilt = true</c> at :5171, THREE statements before <c>Equipment</c> exists, so
///     that flag does not bound this window.
/// The Prefix therefore guards the actual throw conditions rather than a proxy such as
/// <c>IsMount</c>, which the second observation disproves.
///
/// Because the call is <c>OnAgentHit</c>'s last statement, the throw was never functionally
/// destructive — both MissionBehavior loops and the AgentComponent loop have already run, and
/// damage lands earlier in <c>HandleBlow</c>. What it cost was a throw + stack unwind per bite
/// inside <c>OnMissionTick</c>, and a debugger break on every warg engagement.
///
/// The Finalizer is KEPT: Harmony 2.4.2 opens its exception block before the prefix chain, so the
/// generated try/catch genuinely wraps the Prefix, the original and any postfix — it still covers
/// an unanticipated NRE shape. Be precise about its reach, though: it swallows ONLY
/// <see cref="NullReferenceException"/>. The wielded-index getters are unsafe raw pointer
/// dereferences and <c>Agent.Clear</c> zeroes those pointers without nulling <c>Equipment</c>, so
/// a torn-down agent faults with an <see cref="AccessViolationException"/> instead. AVs ARE
/// catchable in this process (the launcher config sets <c>legacyCorruptedStateExceptionsPolicy</c>
/// — see Patch62), but this filter deliberately rethrows them, because an AV is a corrupted-state
/// signal that should reach the crash reporter rather than be silently absorbed. That exposure is
/// identical in unpatched vanilla, whose first statement is the same <c>GetAgentFlags()</c> read.
/// Mirrors the Patch47/48 vanilla-crash-guard pattern.
///
/// Engine parity: this method's body is byte-identical v1.4.5 → v1.4.8 (only line numbers moved).
/// Decision logic lives in <see cref="DropFlaggedItemGuard"/> so it is unit-testable (ADR-008).
/// </summary>
[HarmonyPatch(typeof(Agent), nameof(Agent.CheckToDropFlaggedItem))]
[HarmonyPatchCategory("Patch50_DropFlaggedItemGuard")]
public static class Agent_CheckToDropFlaggedItem_Guard_Patch
{
    /// <summary>Returns false to skip the original when vanilla would dereference null.</summary>
    [HarmonyPrefix]
    public static bool Prefix(Agent __instance)
    {
        // Cheapest check first, and it is also the one that lets us AVOID reads vanilla would do:
        // the wielded-index getters are unsafe raw pointer dereferences
        // (AgentHelper.GetPrimaryWieldedItemIndex), so not touching them on a half-built agent is
        // strictly safer than vanilla, not merely equivalent.
        MissionEquipment equipment = __instance.Equipment;
        if (equipment == null) return false;

        // Vanilla's own first gate. Everything below is unreachable in the engine without it, so
        // mirroring it here keeps the added work off every unarmed agent.
        if (!__instance.GetAgentFlags().HasAnyFlag(AgentFlag.CanWieldWeapon)) return true;

        EquipmentIndex primary = __instance.GetPrimaryWieldedItemIndex();
        EquipmentIndex offhand = __instance.GetOffhandWieldedItemIndex();
        bool primaryWielded = primary != EquipmentIndex.None;
        bool offhandWielded = offhand != EquipmentIndex.None;

        bool wouldThrow = DropFlaggedItemGuard.WouldVanillaDereferenceNull(
            hasEquipment: true,
            primaryWielded, primaryWielded && equipment[primary].Item != null,
            offhandWielded, offhandWielded && equipment[offhand].Item != null);

        return !wouldThrow;
    }

    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception)
        => __exception is NullReferenceException ? null : __exception;
}
