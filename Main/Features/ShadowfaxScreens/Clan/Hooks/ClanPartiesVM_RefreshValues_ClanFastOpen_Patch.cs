using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TAOM.Features.MissionPerf;

namespace TAOM.Features.ShadowfaxScreens.Clan.Hooks;

/// <summary>
/// Clan fast open (docs/reference/harmony-patch-registry.md "Patch127_ShadowfaxClan"). One transpiler swaps the single
/// <c>RefreshPartiesList()</c> call inside <c>ClanPartiesVM.RefreshValues</c> for
/// <see cref="ClanFastOpenCalls.RefreshPartiesListInRefresh"/>, so the skipped build never runs and no postfix of the list
/// build fires for it. The rewrite is the soft-failing <see cref="TickProfilerTranspiler"/>: a call count other than one
/// leaves the whole method vanilla. <see cref="ClanScreenFastPathService.LogInstall"/> reports the stored count at game
/// init. <see cref="Cleanup"/> zeroes it when the apply fails after the transpiler ran.
/// </summary>
[HarmonyPatch(typeof(ClanPartiesVM), nameof(ClanPartiesVM.RefreshValues))]
[HarmonyPatchCategory(ClanFastOpenCalls.PatchCategory)]
public static class ClanPartiesVM_RefreshValues_ClanFastOpen_Patch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = TickProfilerTranspiler.Rewrite(instructions, ClanFastOpenCalls.BuildSwaps(), "ClanPartiesVM.RefreshValues", null, out var swapped);
        ClanFastOpenCalls.LastSwapped = swapped;
        return result;
    }

    // Harmony registers nothing when the apply fails after the transpiler ran, but the transpiler already stored its count.
    [HarmonyCleanup]
    public static void Cleanup(Exception? ex)
    {
        if (ex != null) ClanFastOpenCalls.LastSwapped = 0;
    }
}
