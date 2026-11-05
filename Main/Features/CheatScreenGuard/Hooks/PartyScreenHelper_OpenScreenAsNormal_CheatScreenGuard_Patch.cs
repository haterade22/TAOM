using System;
using System.Collections.Generic;
using HarmonyLib;
using Helpers;
using TAOM.Features.MissionPerf;

namespace TAOM.Features.CheatScreenGuard.Hooks;

/// <summary>
/// Patch_CheatScreenGuard (docs/reference/harmony-patch-registry.md). One transpiler swaps the single Game.get_CheatMode
/// read in PartyScreenHelper.OpenScreenAsNormal (v1.4.8 :185-195) for
/// <see cref="CheatScreenGuardCalls.PartyCheatMode"/>. Soft-failing: any other site count leaves the method vanilla,
/// and the install line names it. No logger is passed; the module's GameInit step writes the line.
/// </summary>
[HarmonyPatch(typeof(PartyScreenHelper), nameof(PartyScreenHelper.OpenScreenAsNormal))]
[HarmonyPatchCategory(CheatScreenGuardModule.PatchCategory)]
public static class PartyScreenHelper_OpenScreenAsNormal_CheatScreenGuard_Patch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = TickProfilerTranspiler.Rewrite(instructions, CheatScreenGuardSwaps.BuildParty(),
            "PartyScreenHelper.OpenScreenAsNormal", null, out var swapped);
        CheatScreenGuardSwaps.LastPartySwapped = swapped;
        return result;
    }

    // An apply that failed after the transpiler ran leaves the method unpatched, so the count must not say gated.
    [HarmonyCleanup]
    public static void Cleanup(Exception ex)
    {
        if (ex != null) CheatScreenGuardSwaps.LastPartySwapped = -1;
    }
}
