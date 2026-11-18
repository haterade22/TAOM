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
/// Patch168: Prefix on the managed wrapper <c>SandBox.MapScene.GetNearestFaceCenterForPositionWithPath</c>.
/// The native search behind it indexes the nav-mesh region map, which the engine fills only when a module named
/// NavalDLC is loaded. TAOM declares NavalDLC incompatible, so the map is null and the search faults with an
/// access violation (TaleWorlds.Native.dll 0x40516c). Vanilla has three managed callers
/// (<c>AIMoveToNearestLandBehavior.AiHourlyTick</c>, <c>MobilePartyAi.GetBestMoveToNearestLandBehavior</c>,
/// <c>MobileParty.MovePartyToTheClosestLand</c>); guarding the wrapper covers all of them. For a valid face the
/// native search is never called and the result is the gate of the nearest town, castle or village (on land),
/// chosen by the pure <see cref="NavalLandFallback.NearestIndex"/>. An invalid face runs the native wrapper, which
/// returns (0,0) before it searches.
/// Thread: <c>GetBestMoveToNearestLandBehavior</c> runs on the engine's AI worker (<c>Campaign.LateAITick</c>), so
/// this prefix must not resolve IoC, read MCM, roll a random number or log more than once.
/// </summary>
[HarmonyPatch(typeof(MapScene), nameof(MapScene.GetNearestFaceCenterForPositionWithPath))]
[HarmonyPatchCategory("Patch168_NavalLandPathfindGuard")]
public static class Patch168_NavalLandPathfindGuard
{
    private static IModLogger? _logger;
    private static volatile bool _logged;

    public static void Initialize(IModLogger logger) => _logger = logger;

    [HarmonyPrefix]
    public static bool Prefix(MapScene __instance, PathFaceRecord pathFaceRecord, ref CampaignVec2 __result)
    {
        // The native wrapper answers an invalid face before it searches (0x62dc63), so vanilla is safe there.
        if (!pathFaceRecord.IsValid())
            return true;

        try
        {
            __result = ResolveLandPoint(__instance, pathFaceRecord);
        }
        catch (Exception ex)
        {
            __result = CampaignVec2.Invalid;
            LogOnce($"land fallback failed ({ex.GetType().Name}: {ex.Message}); returned the invalid land point");
            return false;
        }

        LogOnce($"skipped native GetNearestFaceCenterForPositionWithPath (faults without NavalDLC: no nav-mesh region map); returned {__result}");
        return false;
    }

    private static void LogOnce(string message)
    {
        if (_logged)
            return;
        _logged = true;
        _logger?.LogInfo("[NavalTravel] Patch168: " + message + ".");
    }

    private static CampaignVec2 ResolveLandPoint(MapScene scene, PathFaceRecord face)
    {
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
