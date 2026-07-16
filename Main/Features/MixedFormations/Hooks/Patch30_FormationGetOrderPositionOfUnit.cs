using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;

namespace TAOM.Features.MixedFormations.Hooks;

[HarmonyPatch(typeof(Formation), nameof(Formation.GetOrderPositionOfUnit))]
[HarmonyPatchCategory("Patch30_MixedFormations")]
public static class Patch30_FormationGetOrderPositionOfUnit
{
    // Cached service reference — the Prefix fires per-unit-per-formation-position-recalculation
    // (up to 40,000× per frame in worst-case 200-unit formations). DryIoc singleton resolves are
    // fast, but caching the singleton in a static field skips the dict lookup entirely. Per
    // .claude/rules/harmony-patches.md hot-path reflection caching pattern.
    private static IFormationLayoutService? _service;

    [HarmonyPrefix]
    public static bool Prefix(Formation __instance, Agent unit, ref WorldPosition __result)
    {
        try
        {
            // Open-field-only: skip all mixed-formation repositioning in siege / sally-out / hideout /
            // naval / settlement missions (Mission.IsFieldBattle is FALSE for all of them). Returning
            // true lets vanilla GetOrderPositionOfUnit compute the slot. Placed first to short-circuit
            // this per-unit hot path (~40,000×/frame) before any IoC resolve or adapter allocation.
            if (Mission.Current?.IsFieldBattle != true) return true;

            var service = _service ??= IoC.Resolve<IFormationLayoutService>();
            if (service == null) return true;

            var formation = new FormationAdapter(__instance);
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
        catch
        {
            return true;
        }
    }
}
