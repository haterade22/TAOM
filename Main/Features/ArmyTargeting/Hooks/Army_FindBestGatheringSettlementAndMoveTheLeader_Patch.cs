using System;
using HarmonyLib;
using TAOM.Features.ArmyTargeting.Diagnostics;
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
/// The Finalizer swallows ONLY <see cref="NullReferenceException"/>. Net effect: the broken army
/// skips relocating its gathering leader this tick (vanilla already null-guards
/// <c>AiBehaviorObject</c> downstream — Army.cs:480-490/564) and re-plans next tick — strictly
/// better than a CTD. Mirrors the Patch47/48 vanilla-crash-guard pattern.
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

    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception, Army __instance, Settlement focusSettlement)
    {
        if (!(__exception is NullReferenceException)) return __exception;

        try
        {
            _diagnostics ??= IoC.Resolve<ISiegeGatheringDiagnosticsService>();
            _diagnostics?.Record(SiegeGatheringFailureInfo.FromArmy(__instance, focusSettlement));
        }
        catch
        {
            // IoC not ready / diagnostics failure must never re-throw out of a Finalizer.
        }

        return null; // swallow — the map tick continues, no CTD
    }
}
