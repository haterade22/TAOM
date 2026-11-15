using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.MountAndBlade.View;

namespace TAOM.Features.MountReins.Hooks;

/// <summary>
/// Patch153 (player case 2026-11-15, #1081 RCA row 12): transpiler on public static
/// <c>MountVisualCreator.AddMountMesh(MBAgentVisuals, ItemObject, ItemObject, string, Agent)</c> (v1.5.7). A harness
/// with reins on a warg, fell warg, dire wolf or spider made the engine build cloth reins on a mount with no rein
/// skeleton; a player's hero on a dire wolf with the Saddle of Aeneas crashed at every mounted battle spawn. The one
/// cloth call is swapped for <see cref="MountReinsHook.AddReinsCloth"/>. Callers, thread, PatchShield and co-op:
/// docs/reference/harmony-patch-registry.md "Patch153_MountReinsGuard".
/// </summary>
[HarmonyPatch(typeof(MountVisualCreator), nameof(MountVisualCreator.AddMountMesh))]
[HarmonyPatchCategory(MountReinsModule.PatchCategory)]
[ClientOnly]
public static class Patch153_MountReinsGuard
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => MountReinsTranspiler.SwapReinsCloth(instructions, MountReinsModule.Logger);
}
