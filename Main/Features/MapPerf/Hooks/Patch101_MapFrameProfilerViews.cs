using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace TAOM.Features.MapPerf.Hooks;

// Patch101 views category (default off; applied once per process by MapFrameProfilerInstaller, separate from
// the core category so a failing view cannot stop the frame boundary). Targets every TAOM MapView subclass's
// own override of a per-frame virtual (OnFrameTick, OnMapScreenUpdate, OnMenuModeTick, OnIdleTick), found
// by reflection; at v1.5.3 that is the three OnMapScreenUpdate overrides. MapScreen ticks the views through
// compiler-generated closures (ForeachReverse), so each TAOM view is timed on its own override instead; the
// entry is keyed by the view's type and counts toward taomMs. Main thread.

[HarmonyPatch]
[HarmonyPatchCategory(MapFrameProfilerInstaller.ViewsCategory)]
public static class MapView_PerFrame_MapProfiler_Patch
{
    public static IEnumerable<MethodBase> TargetMethods() => MapProfilerTargets.MapViewTargets();

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    public static void Prefix(out ProbeStamp __state) => __state = MapFrameProfilerHooks.BeginProbe();

    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    public static void Postfix(object __instance, ProbeStamp __state) => MapFrameProfilerHooks.RecordView(__instance, __state);
}
