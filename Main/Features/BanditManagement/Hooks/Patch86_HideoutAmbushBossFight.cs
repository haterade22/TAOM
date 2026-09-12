using System;
using System.Collections.Generic;
using HarmonyLib;
using SandBox.Missions.MissionLogics.Hideout;
using TaleWorlds.Core;

namespace TAOM.Features.BanditManagement.Hooks;

/// <summary>
/// Patch86 (sneak-in route) — Prefix on the private instance
/// <c>HideoutAmbushMissionController.SpawnRemainingTroopsForBossFight(List&lt;MatrixFrame&gt; spawnFrames, int spawnCount)</c>
/// (SandBox.dll, v1.4.8 :478-555; called once per night hideout, from <c>SpawnBossAndBodyguards</c>
/// :811 inside the boss-fight cinematic callback).
///
/// <c>InitializeTroops</c> (:751-778) supplies every enemy troop up front and keeps the non-hero,
/// non-boss ones in the private <c>_allEnemyTroops</c>; the stealth phase pops sentries from it.
/// At the boss fight the caller computes <c>spawnCount = Clamp(pop / 2, 4, 20)</c> (:810) and this
/// method pads the list UP to that number (:507-511) then spawns every element (:528-542): the
/// clamp is a floor, and the fight is the boss plus everyone who was not a sentry. This prefix
/// trims the list to the <see cref="IHideoutBossFightService.BodyguardCount"/> highest-level
/// troops and sets <c>spawnCount</c> to match, then lets the original run: it pads from
/// <c>_allEnemyTroopTypesCache</c> when fewer remain (so a thin hideout still fields boss + N) and
/// spawns the boss from his own origin (:513-527), which is never in this list.
///
/// The original always runs (<c>return true</c>), so no vanilla gate is dropped. The selection is
/// computed before the list is touched, and the mutation is a <c>Clear</c> + <c>AddRange</c> that
/// cannot throw, so a failure anywhere above leaves vanilla's list intact.
///
/// <c>spawnFrames</c> is deliberately NOT declared: the prefix does not need it, and a
/// <c>MatrixFrame</c> parameter would raise the deferred-category question for nothing.
/// </summary>
[HarmonyPatch(typeof(HideoutAmbushMissionController), "SpawnRemainingTroopsForBossFight")]
[HarmonyPatchCategory(Patch86_HideoutBossFight.Category)]
public static class Patch86_HideoutAmbushBossFight
{
    // Harmony injects a private field as `___` (three underscores) + the field's LITERAL name.
    // TaleWorlds fields carry a leading underscore, so `_allEnemyTroops` is `____allEnemyTroops`
    // (FOUR). Three would ask for a field named `allEnemyTroops` and fail the WHOLE category at
    // apply time (Patch46, RCA 2026-06-09). Do not "fix" the count. HarmonyFieldInjectionNamingTests
    // checks it; Patch86HideoutBossFightBindingTests pins the field types.
    [HarmonyPrefix]
    public static bool Prefix(
        ref int spawnCount,
        List<IAgentOriginBase> ____allEnemyTroops,
        IAgentOriginBase ____overriddenHideoutBossAgentOrigin,
        List<IAgentOriginBase> ____allEnemyTroopTypesCache)
    {
        try
        {
            var service = Patch86_HideoutBossFight.Service;
            if (service == null || ____allEnemyTroops == null)
                return true;

            var levels = new List<int>(____allEnemyTroops.Count);
            foreach (var origin in ____allEnemyTroops)
                levels.Add(origin?.Troop?.Level ?? 0);

            // canPad: vanilla's GetNewRandomEnemyTroop (:473) derefs GetRandomElement on the type
            // cache, so a spawn count above the kept count is only safe when the cache has entries.
            var selection = service.PlanAmbush(
                levels,
                hasBossOrigin: ____overriddenHideoutBossAgentOrigin != null,
                canPad: (____allEnemyTroopTypesCache?.Count ?? 0) > 0);

            var kept = new List<IAgentOriginBase>(selection.KeepIndices.Count);
            foreach (var index in selection.KeepIndices)
                kept.Add(____allEnemyTroops[index]);

            var unspawned = ____allEnemyTroops.Count;
            ____allEnemyTroops.Clear();
            ____allEnemyTroops.AddRange(kept);
            spawnCount = selection.SpawnCount;

            Patch86_HideoutBossFight.Logger?.LogInfo(
                $"[Patch86] hideout sneak-in boss fight: kept {kept.Count} of {unspawned} unspawned, " +
                $"spawn count {selection.SpawnCount} (bodyguards {service.BodyguardCount})");
        }
        catch (Exception ex)
        {
            try
            {
                Patch86_HideoutBossFight.Logger?.LogError(
                    $"[Patch86] sneak-in trim failed, vanilla spawns its full list for this hideout: {ex}");
            }
            catch { /* never throw out of a prefix */ }
        }
        return true;
    }
}
