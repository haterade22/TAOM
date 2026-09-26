using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.TrollBruteForce.Hooks;

/// <summary>
/// Spaces a troll formation for trolls. The engine spaces every foot unit for a 0.76 m human
/// (<c>Formation.UnitDiameter</c> is <c>BipedalRadius</c> x 2 whatever the Monster), so a line of trolls stood inside
/// each other. This postfix raises the width to the one <see cref="TrollFormationSpacingStore"/> stores for the
/// formation; the line arrangement reads <c>owner.UnitDiameter</c> for every slot, file count and rank depth, and
/// MixedFormations reads it through <c>IFormationAdapter.UnitDiameter</c>.
/// The getter also runs on the asynchronous AI thread and the TWParallel workers
/// (<c>Formation.GetOrderPositionOfUnit</c>), so the postfix only reads a concurrent map; the tracker writes it on
/// the main thread. No try/catch: the postfix's only engine access is the public readonly <c>Formation.Team</c>
/// field read, which cannot throw, and the store calls no engine code at all.
/// </summary>
[HarmonyPatch(typeof(Formation), nameof(Formation.UnitDiameter), MethodType.Getter)]
[HarmonyPatchCategory("Patch92_TrollFormationSpacing")]
public static class Patch92_TrollFormationSpacing
{
    [HarmonyPostfix]
    public static void Postfix(Formation __instance, ref float __result)
    {
        bool isSimulationCopy = __instance.Team == null;
        if (TrollFormationSpacingStore.TryGet(__instance, isSimulationCopy, out float diameter) && diameter > __result)
            __result = diameter;
    }
}

/// <summary>
/// The order preview, the deployment placement and the spawn frames lay a formation out on a team-less copy: the
/// real formation's <c>GetUnitPositionWithIndexAccordingToNewOrder</c> overloads and <c>GetUnitSpawnFrameWithIndex</c>
/// hand a <c>new Formation(null, -1)</c> to one private static layout (v1.5.3 <c>Formation.cs:1680-1698, 3097</c>).
/// The copy is in no store, so trolls were placed at human width and their capsules pushed them to touching (every
/// neighbour 1.6 m apart, 2026-09-25). This scope names the real formation for the duration of the call, on the
/// calling thread, so the copy takes its width.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch92_TrollFormationSpacing")]
public static class Patch92_TrollFormationSpacingSimulation
{
    internal static IEnumerable<MethodBase> TargetMethods() =>
        typeof(Formation).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name == nameof(Formation.GetUnitPositionWithIndexAccordingToNewOrder)
                || m.Name == nameof(Formation.GetUnitSpawnFrameWithIndex));

    [HarmonyPrefix]
    public static void Prefix(Formation __instance, out object? __state)
    {
        __state = TrollFormationSpacingStore.LayingOut;
        TrollFormationSpacingStore.LayingOut = __instance;
    }

    [HarmonyFinalizer]
    public static void Finalizer(object? __state) => TrollFormationSpacingStore.LayingOut = __state;
}
