using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.Hooks;

/// <summary>
/// Severs the rider↔spider mount link an instant before native <c>Agent.Die</c> runs.
///
/// WHY: a rider dying while mounted on the spider AVs inside native Die() (2026-06-11 RCA,
/// docs/features/spider.md). Every observable data surface was equalized with the working
/// mounts (action sets, usage surface, typed falls/verbs, reins, family, goblin-set
/// inheritance — all engine-probe-verified) and the mount is provably healthy at the crash
/// instant (Active, full HP, link intact, gait data readable). The remaining failure is
/// internal to the engine's mounted-death path for non-vanilla mounts, so we route around it:
/// dismounted riders die on the proven on-foot path; a dying spider frees its rider first so
/// the mount dies riderless (the authored falls path). Vanilla mounts are untouched.
///
/// The hard dismount is the engine's own private <c>SetMountAgent(null)</c> (the native
/// link-severing call behind the MountAgent property setter), cached via AccessTools at
/// Initialize per harmony-patches.md (no per-call reflection).
/// </summary>
[HarmonyPatch(typeof(Agent), nameof(Agent.Die))]
[HarmonyPatchCategory("Patch47_SpiderDeathDismount")]
public static class Agent_Die_SpiderDismount_Patch
{
    private static System.Reflection.MethodInfo _setMountAgent;
    private static ISpiderAttackService _service;

    public static void Initialize()
    {
        _setMountAgent = AccessTools.Method(typeof(Agent), "SetMountAgent");
    }

    [HarmonyPrefix]
    public static void Prefix(Agent __instance)
    {
        try
        {
            var svc = _service ??= IoC.Resolve<ISpiderAttackService>();
            if (svc == null || _setMountAgent == null) return;

            // Rider dying on a spider -> hard-dismount so native Die takes the on-foot path.
            Agent mount = __instance.MountAgent;
            if (mount != null && svc.IsSpiderMonster(mount.Monster?.StringId))
            {
                _setMountAgent.Invoke(__instance, new object[] { null });
                return;
            }

            // Spider mount dying with a rider -> free the rider first so the mount dies
            // riderless (the authored falls path) instead of the untested ridden-death path.
            if (svc.IsSpiderMonster(__instance.Monster?.StringId))
            {
                Agent rider = __instance.RiderAgent;
                if (rider != null)
                {
                    _setMountAgent.Invoke(rider, new object[] { null });
                }
            }
        }
        catch
        {
            // Guard must never turn a death into a crash of its own; native Die proceeds as-is.
        }
    }
}
