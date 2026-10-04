using System;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MixedFormations.Hooks;

[HarmonyPatch(typeof(Formation), nameof(Formation.GetOrderPositionOfUnit))]
[HarmonyPatchCategory("Patch30_MixedFormations")]
public static class Patch30_FormationGetOrderPositionOfUnit
{
    // Cached service reference. The prefix runs about twice a second for every AI unit in a formation
    // (Agent.TickParallel's 0.45 to 0.55 s formation timer, on the TWParallel worker threads), plus one call
    // per unit whenever the engine forces a formation's values and per unit of the player's order preview
    // on the main thread. Caching the singleton skips the container lookup
    // (.claude/rules/harmony-patches.md hot-path caching pattern).
    private static IFormationLayoutService? _service;

    [HarmonyPrefix]
    public static bool Prefix(Formation __instance, Agent unit, ref WorldPosition __result)
    {
        try
        {
            // Open-field-only: skip all mixed-formation repositioning in siege / sally-out / hideout /
            // naval / settlement missions (Mission.IsFieldBattle is FALSE for all of them). Returning
            // true lets vanilla GetOrderPositionOfUnit compute the slot. Placed first so a
            // siege, hideout or naval mission returns before the service is resolved.
            if (Mission.Current?.IsFieldBattle != true) return true;

            // Banner bearers own their slot. BannerBearerLogic places them via SwitchUnitLocations
            // into the engine's dedicated banner positions (RelativeFormationPosition[6] tables);
            // returning a mixed-formation position for them instead would scatter the standards
            // through the ranks. Returning true lets vanilla place the bearer. Cheap enough for
            // this hot path — Agent.Banner is Equipment?.GetBanner(), a single _weaponSlots[4]
            // read with no loop or allocation. Codex review 2026-07-16 MED (BannerBearers).
            if (unit?.Banner != null) return true;

            var service = _service ??= IoC.Resolve<IFormationLayoutService>();
            if (service == null) return true;

            // Worker-thread fast path: a formation with no layout (every formation outside the player's team,
            // and every player formation that is not mixed) goes straight back to vanilla. The service answers
            // with no lock, no allocation and no FormationQuerySystem read, and hands back the adapter the
            // main thread built when it assigned the layout.
            var formation = service.FindLaidOutFormation(__instance);
            if (formation == null) return true;
            var agentIsRanged = unit?.Character?.IsRanged ?? false;
            var agentIndex = unit?.Index ?? -1;
            if (agentIndex < 0) return true;

            var planePosition = service.ComputeUnitPlanePosition(formation, agentIndex, agentIsRanged);
            if (!planePosition.HasValue) return true;

            var scene = Mission.Current?.Scene;
            if (scene == null) return true;

            var p = planePosition.Value;
            var probe = new Vec3(p.x, p.y, 0f, -1f);
            var groundHeight = scene.GetGroundHeightAtPosition(probe, BodyFlags.CommonCollisionExcludeFlags);
            var candidate = new WorldPosition(scene, new Vec3(p.x, p.y, groundHeight, -1f));

            // Codex review #35 finding 1 (HIGH): vanilla Hold path (Formation.GetOrderPositionOfUnitAux)
            // routes through Mission.IsFormationUnitPositionAvailable to validate the candidate is on
            // the navmesh and not blocked by walls/cliffs/siege props/map boundaries. Skipping this
            // check would order units onto non-navigable terrain. Match vanilla's behavior: if the
            // candidate is unavailable, fall through to vanilla so the engine can fall back to
            // unit.GetWorldPosition() (the unit just stays where it is).
            var mission = unit?.Mission ?? Mission.Current;
            var team = __instance?.Team;
            if (mission != null && team != null)
            {
                var checkPos = candidate;
                if (!mission.IsFormationUnitPositionAvailable(ref checkPos, team))
                    return true;
            }

            __result = candidate;
            return false;
        }
        catch (Exception ex)
        {
            // Vanilla places this unit. The service logs the first throw of each mission in full and counts the
            // rest for its mission-end summary, so a fault on the worker threads never floods the log.
            try { (_service ?? IoC.Resolve<IFormationLayoutService>())?.NoteFallback(ex); }
            catch { /* never throw from the worker-thread prefix */ }
            return true;
        }
    }
}
