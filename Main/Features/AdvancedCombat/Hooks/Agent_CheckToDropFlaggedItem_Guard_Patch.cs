using System;
using HarmonyLib;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;
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
/// The Finalizer is KEPT, and it no longer hides what it catches. Since the Prefix skips both vanilla
/// null states, any NRE it sees is not the vanilla one: it comes from <c>DropItem</c>, the missile
/// callbacks or another mod's patch (Harmony 2.4.2 opens its try block before the prefixes). Every TAOM
/// synthetic-blow route already catches before <c>OnMissionTick</c> (<c>BehaviorTree.RunTree</c>,
/// <c>WargAttackService</c>, <c>SpiderAttackService</c>), but at a
/// cost: <c>RunTree</c>'s catch stops that creature's tree for the rest of the mission. The Finalizer
/// absorbs such an NRE at <c>OnAgentHit</c>'s last statement without that cost, on vanilla's mount,
/// dismount and hit paths too, and logs the first one per process at WARNING with its stack (NRE guard
/// audit 2026-10-27; Mike chose keep-and-log over delete). It swallows ONLY
/// <see cref="NullReferenceException"/>; anything else goes back with its throw site kept. A torn-down
/// agent faults with an <see cref="AccessViolationException"/> in the wielded-index getters (unsafe
/// raw pointer reads; <c>Agent.Clear</c> zeroes them without nulling <c>Equipment</c>); this filter
/// rethrows it, and on the TAOM routes the catches above then absorb it, as they would in vanilla.
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

    private static IModLogger _logger;
    private static int _reported;

    // TAOM.Tests sets the logger; at runtime it resolves lazily from IoC on the first swallow.
    internal static void UseLogger(IModLogger logger)
    {
        _logger = logger;
        _reported = 0;
    }

    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception)
    {
        if (!(__exception is NullReferenceException))
            return RethrowStackPreserver.PreserveForRethrow(__exception, null);

        ReportFirst(__exception);
        return null;
    }

    // Once per process: the target runs on every hit, mount and dismount, and callbacks can arrive off
    // the main thread, so the gate is an interlocked flag.
    private static void ReportFirst(Exception exception)
    {
        if (System.Threading.Interlocked.Exchange(ref _reported, 1) != 0) return;
        try
        {
            _logger ??= IoC.Resolve<IModLogger>();
            _logger?.LogWarning(
                "[AdvancedCombat] Patch50 swallowed an NRE in Agent.CheckToDropFlaggedItem that is not the " +
                "vanilla null state its prefix prevents (first one this session; later ones are not logged): " +
                exception);
        }
        catch
        {
            // A failed log line must never undo the swallow.
        }
    }
}
