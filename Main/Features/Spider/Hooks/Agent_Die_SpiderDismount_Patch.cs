using HarmonyLib;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.Hooks;

/// <summary>
/// Severs the rider↔creature mount link an instant before native <c>Agent.Die</c> runs (spider first, every
/// creature mount since 2026-11-08, see <see cref="CreatureMounts"/>).
///
/// WHY: a rider dying while mounted on the spider AVs inside native Die() (2026-06-11 RCA,
/// docs/features/spider.md). Every observable data surface was equalized with the working
/// mounts (action sets, usage surface, typed falls/verbs, reins, family, goblin-set
/// inheritance — all engine-probe-verified) and the mount is provably healthy at the crash
/// instant (Active, full HP, link intact, gait data readable). The remaining failure is
/// internal to the engine's mounted-death path for non-vanilla mounts, so we route around it:
/// dismounted riders die on the proven on-foot path; a dying creature mount frees its rider first so
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

    public static void Initialize()
    {
        // Pinned by ReflectionSiteBindingTests: a rename at an engine bump would switch the guard off for
        // every creature mount, so say so once instead of failing silent.
        _setMountAgent = AccessTools.Method(typeof(Agent), "SetMountAgent", new[] { typeof(Agent) });
        if (_setMountAgent == null)
            TaleWorlds.Library.Debug.Print("[TAOM] Patch47: Agent.SetMountAgent(Agent) not found; creature riders die mounted, unguarded",
                0, TaleWorlds.Library.Debug.DebugColor.Red);
    }

    [HarmonyPrefix]
    public static void Prefix(Agent __instance)
    {
        try
        {
            if (_setMountAgent == null) return;

            // Every creature mount, not just the spider (CreatureMounts says why): a v2.0.29.5 player CTD
            // (2026-11-07) died in native Die from a warg bite while this guard was spider-only.
            // Rider dying on a creature -> hard-dismount so native Die takes the on-foot path.
            Agent mount = __instance.MountAgent;
            if (mount != null && CreatureMounts.IsCreatureMount(mount.Monster?.StringId))
            {
                _setMountAgent.Invoke(__instance, new object[] { null });
                return;
            }

            // Creature mount dying with a rider -> free the rider first so the mount dies
            // riderless (the authored falls path) instead of the untested ridden-death path.
            // Rider first: only a mount has one, so the predicate is only ever asked about a mount.
            // Cost, unverified in game: native die's ridden-death block (RVA 0x5ED980 on v1.5.6) gives the surviving
            // rider a fall action, timers and a re-check of the wielded usage; after this sever it gets none of them.
            Agent rider = __instance.RiderAgent;
            if (rider != null && CreatureMounts.IsCreatureMount(__instance.Monster?.StringId))
            {
                _setMountAgent.Invoke(rider, new object[] { null });
            }
        }
        catch
        {
            // Guard must never turn a death into a crash of its own; native Die proceeds as-is.
        }
    }
}
