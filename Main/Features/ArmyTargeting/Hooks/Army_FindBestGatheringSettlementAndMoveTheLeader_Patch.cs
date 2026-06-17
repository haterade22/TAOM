using System;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.ArmyTargeting.Hooks;

/// <summary>
/// Swallows a vanilla <see cref="NullReferenceException"/> in
/// <c>Army.FindBestGatheringSettlementAndMoveTheLeader</c>.
///
/// WHY: when an AI besieger army cannot resolve a gathering fortification — every
/// <c>Kingdom.Settlements</c> fortification is under siege / out of range AND
/// <c>SettlementHelper.FindNearestFortificationToMobileParty</c> returns null — vanilla
/// dereferences <c>settlement.GatePosition</c> with no null guard
/// (TaleWorlds.CampaignSystem.Army.cs:726, v1.4.6). It fires on the map tick that starts the
/// siege (Army.OnSiegeStarted), crashing the game while the player walks / fast-forwards. A null
/// <c>Kingdom</c> (army leader's clan not in a kingdom) throws in the same method at Army.cs:659.
/// The crash report (2026-06-17) shows NO TAOM patch anywhere on the stack — this is a vanilla
/// missing-null-guard that TAOM's aggressive cross-map siege targeting (Patch22_ArmyTargeting)
/// makes more reachable, not a defect TAOM introduces.
///
/// The Finalizer swallows ONLY <see cref="NullReferenceException"/>. Net effect: the broken army
/// skips relocating its gathering leader this tick (vanilla already null-guards
/// <c>AiBehaviorObject</c> downstream — Army.cs:480-490/564) and re-plans next tick — strictly
/// better than a CTD. Mirrors the Patch47/48 vanilla-crash-guard pattern.
/// </summary>
[HarmonyPatch(typeof(Army), "FindBestGatheringSettlementAndMoveTheLeader")]
[HarmonyPatchCategory("Patch49_ArmyGatheringNreGuard")]
public static class Army_FindBestGatheringSettlementAndMoveTheLeader_Patch
{
    private static IModLogger _logger;

    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception)
    {
        if (!(__exception is NullReferenceException)) return __exception;

        try
        {
            _logger ??= IoC.Resolve<IModLogger>();
            _logger?.LogDebug(
                "ArmyGatheringGuard: suppressed vanilla NRE in Army.FindBestGatheringSettlementAndMoveTheLeader " +
                "(army has no resolvable gathering fortification).");
        }
        catch
        {
            // IoC not ready / logging failure must never re-throw out of a Finalizer.
        }

        return null; // swallow — the map tick continues, no CTD
    }
}
