using System;
using System.Collections.Generic;
using HarmonyLib;
using Helpers;
using TAOM.Features.MissionPerf;

namespace TAOM.Features.CheatScreenGuard.Hooks;

/// <summary>
/// Patch_CheatScreenGuard (docs/reference/harmony-patch-registry.md). One transpiler swaps the single Game.get_CheatMode
/// read in the private InventoryScreenHelper.OpenInventoryPresentation (v1.4.8 :174-196) for
/// <see cref="CheatScreenGuardCalls.InventoryCheatMode"/>. Soft-failing: any other site count leaves the method vanilla,
/// and the install line names it. No logger is passed; the module's GameInit step writes the line.
/// </summary>
[HarmonyPatch(typeof(InventoryScreenHelper), "OpenInventoryPresentation")]
[HarmonyPatchCategory(CheatScreenGuardModule.PatchCategory)]
public static class InventoryScreenHelper_OpenInventoryPresentation_CheatScreenGuard_Patch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = TickProfilerTranspiler.Rewrite(instructions, CheatScreenGuardSwaps.BuildInventory(),
            "InventoryScreenHelper.OpenInventoryPresentation", null, out var swapped);
        CheatScreenGuardSwaps.LastInventorySwapped = swapped;
        return result;
    }

    // An apply that failed after the transpiler ran leaves the method unpatched, so the count must not say gated.
    [HarmonyCleanup]
    public static void Cleanup(Exception ex)
    {
        if (ex != null) CheatScreenGuardSwaps.LastInventorySwapped = -1;
    }
}
