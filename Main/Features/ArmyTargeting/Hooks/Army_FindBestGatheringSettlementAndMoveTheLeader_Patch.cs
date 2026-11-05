using System;
using HarmonyLib;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;
using TAOM.Features.ArmyTargeting.Diagnostics;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.ArmyTargeting.Hooks;

/// <summary>
/// Swallows a vanilla <see cref="NullReferenceException"/> in
/// <c>Army.FindBestGatheringSettlementAndMoveTheLeader</c>.
///
/// WHY: when an AI besieger army cannot resolve a gathering fortification — every
/// <c>Kingdom.Settlements</c> fortification is under siege / out of range AND
/// <c>SettlementHelper.FindNearestFortificationToMobileParty</c> returns null — vanilla
/// dereferences <c>settlement.GatePosition</c> with no null guard
/// (TaleWorlds.CampaignSystem.Army.cs:726 — verified STILL unguarded in v1.4.7). It fires on the
/// map tick that starts the siege (Army.OnSiegeStarted), crashing the game while the player walks /
/// fast-forwards. A null <c>Kingdom</c> (army leader's clan not in a kingdom) throws in the same
/// method at Army.cs:659 (also unchanged in v1.4.7). The crash report (2026-06-17) shows NO TAOM
/// patch anywhere on the stack — this is a vanilla missing-null-guard that TAOM's aggressive
/// cross-map siege targeting (Patch22_ArmyTargeting) makes more reachable, not a defect TAOM
/// introduces.
///
/// v1.4.7 shipped an "AI behaviour null reference" crash fix, but decompiling this method on v1.4.7
/// shows both derefs above are UNCHANGED (same lines, no guard added) — that fix targeted a
/// different site, so this guard remains load-bearing. See docs/migration/v1.4.7-impact.md.
///
/// The Finalizer swallows a <see cref="NullReferenceException"/> only when vanilla's body was entered
/// AND the army is in one of those two dead ends (<see cref="ShouldContain"/>; v1.4.8 lines :659 and
/// :726). The exception type alone would also swallow an NRE from a callee or another mod's patch and
/// file it as a siege dead end (NRE guard audit 2026-10-27). The state alone is not enough either: Harmony
/// opens the try block before the prefixes, so a foreign prefix that throws while the army is in a dead
/// end matched it (Codex 2026-10-27). So a <c>Priority.Last</c> prefix sets Harmony's <c>__state</c>
/// just before vanilla's body; a prefix that throws first leaves it false. A foreign postfix cannot
/// match, because in a dead end vanilla's body throws. Left: a foreign transpiler that inserts a throw
/// into the body. A prefix cannot predict :726 without repeating the scoring loop, so the containment
/// stays a finalizer. Every other exception goes back with its throw site kept. Net effect of a swallow:
/// the army skips relocating its gathering leader this tick and re-plans next tick. Vanilla guards
/// <c>AiBehaviorObject</c> at :480 and :564 but reads it unguarded at :518, :574-605 and :753; whether a
/// contained :726 reaches those reads is unverified.
///
/// DIAGNOSTICS: before swallowing, it records the army/kingdom/focus-settlement context to
/// <see cref="ISiegeGatheringDiagnosticsService"/> so the dead-end sieges become a reviewable
/// to-fix list (one deduplicated WARNING per problem siege) instead of a silent breadcrumb. The
/// whole diagnostic path is inside the try/catch — if it throws, the NRE is still suppressed, so
/// the crash guard is never weakened. Harmony injects <c>__instance</c> (the Army) and the original
/// <c>focusSettlement</c> argument into the finalizer.
/// </summary>
[HarmonyPatch(typeof(Army), "FindBestGatheringSettlementAndMoveTheLeader")]
[HarmonyPatchCategory("Patch49_ArmyGatheringNreGuard")]
public static class Army_FindBestGatheringSettlementAndMoveTheLeader_Patch
{
    private static ISiegeGatheringDiagnosticsService _diagnostics;

    // Runs after every other prefix, so true means vanilla's body was about to start.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    public static void Prefix(out bool __state) => __state = true;

    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception, Army __instance, Settlement focusSettlement, bool __state)
    {
        if (!ShouldContain(__exception, __state, () => ReachedKnownDeadEnd(__instance)))
            return RethrowStackPreserver.PreserveForRethrow(__exception, null);

        try
        {
            _diagnostics ??= IoC.Resolve<ISiegeGatheringDiagnosticsService>();
            _diagnostics?.Record(SiegeGatheringFailureInfo.FromArmy(__instance, focusSettlement));
        }
        catch (Exception ex)
        {
            // IoC not ready / diagnostics failure must never re-throw out of a Finalizer.
            Outcomes.Swallowed("ArmyTargeting", "Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.Finalizer", ex);
        }

        return null; // swallow: the map tick continues, no CTD
    }

    /// <summary>An NRE from vanilla's own body, in one of the two dead ends. The state is read only then.</summary>
    internal static bool ShouldContain(Exception exception, bool bodyEntered, Func<bool> deadEnd) =>
        exception is NullReferenceException && bodyEntered && deadEnd();

    /// <summary>
    /// The two states vanilla throws in: a kingdomless army whose leader is active reads
    /// <c>Kingdom.Settlements</c> (:659); with no scored settlement, vanilla sets <c>AiBehaviorObject</c>
    /// to the null nearest-fortification fallback (:725) and reads its <c>GatePosition</c> (:726). The
    /// fallback search runs only when the objective is missing.
    /// </summary>
    internal static bool IsKnownDeadEnd(bool kingdomMissing, bool leaderActive, bool objectiveMissing, Func<bool> fallbackMissing) =>
        (kingdomMissing && leaderActive) || (objectiveMissing && fallbackMissing());

    // Exception path only. Any doubt (an unreadable army) means not a known dead end, so it rethrows.
    private static bool ReachedKnownDeadEnd(Army army)
    {
        try
        {
            var leader = army?.LeaderParty;
            if (leader == null) return false;
            return IsKnownDeadEnd(
                kingdomMissing: army.Kingdom == null,
                leaderActive: leader.LeaderHero?.IsActive == true,
                objectiveMissing: army.AiBehaviorObject == null,
                fallbackMissing: () => SettlementHelper.FindNearestFortificationToMobileParty(leader, leader.NavigationCapability) == null);
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("ArmyTargeting", "Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.ReachedKnownDeadEnd", ex);
            return false;
        }
    }
}
