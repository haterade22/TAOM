using System;
using System.Collections.Generic;
using HarmonyLib;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.NavalTravel.Hooks;

/// <summary>
/// Patch159 — Prefix on the managed wrapper <c>SandBox.MapScene.GetNearestFaceCenterForPositionWithPath</c>.
/// The native search behind it indexes the nav-mesh region map, which the engine fills only when a module named
/// NavalDLC is loaded; TAOM never loads it, so the search faults with an access violation (TaleWorlds.Native.dll 0x40516c), which no managed Finalizer can catch (#296, #120). Vanilla
/// has three managed callers (<c>AIMoveToNearestLandBehavior.AiHourlyTick</c>,
/// <c>MobilePartyAi.GetBestMoveToNearestLandBehavior</c>, <c>MobileParty.MovePartyToTheClosestLand</c>);
/// guarding the wrapper covers all of them. Without NavalDLC the map is always null, so the guard is unconditional:
/// the native method is never called and the result is the gate of the nearest town, castle or village
/// (on land). The anchor choice is the pure <see cref="NavalLandFallback.NearestIndex"/>.
/// Any failure leaves <c>__result</c> as a land point where possible and never calls the native method.
/// </summary>
[HarmonyPatch(typeof(MapScene), nameof(MapScene.GetNearestFaceCenterForPositionWithPath))]
[HarmonyPatchCategory("Patch159_NavalLandPathfindGuard")]
public static class Patch159_NavalLandPathfindGuard
{
    private static IModLogger? _logger;
    private static bool _logged;

    public static void Initialize(IModLogger logger) => _logger = logger;

    [HarmonyPrefix]
    public static bool Prefix(MapScene __instance, PathFaceRecord pathFaceRecord, ref CampaignVec2 __result)
    {
        try
        {
            __result = ResolveLandPoint(__instance, pathFaceRecord);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[NavalTravel] Patch159: land fallback failed ({ex.GetType().Name}: {ex.Message}); returning the invalid land point.");
            __result = CampaignVec2.Invalid;
        }

        if (!_logged)
        {
            _logged = true;
            _logger?.LogInfo("[NavalTravel] Patch159: skipped native GetNearestFaceCenterForPositionWithPath (crashes on TAOM_Map), returned the nearest settlement gate.");
        }
        return false;
    }

    private static CampaignVec2 ResolveLandPoint(MapScene scene, PathFaceRecord face)
    {
        // The native wrapper also answers an invalid face without searching.
        if (!face.IsValid())
            return CampaignVec2.Invalid;
        Vec2 from = scene.GetNavigationMeshCenterPosition(face);

        var all = Settlement.All;
        var settlements = new List<Settlement>(all.Count);
        var anchors = new List<(float X, float Y)>(all.Count);
        foreach (var s in all)
        {
            if (!(s.IsTown || s.IsCastle || s.IsVillage)) continue;
            var gate = s.GatePosition.ToVec2();
            settlements.Add(s);
            anchors.Add((gate.X, gate.Y));
        }

        var i = NavalLandFallback.NearestIndex(from.X, from.Y, anchors);
        return i < 0 ? CampaignVec2.Invalid : settlements[i].GatePosition;
    }
}
