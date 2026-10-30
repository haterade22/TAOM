using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;
using TAOM.Features.MissionPerf;

namespace TAOM.Features.ShadowfaxScreens.Clan.Hooks;

/// <summary>
/// Clan fast open (docs/reference/harmony-patch-registry.md "Patch127_ShadowfaxClan"). One transpiler swaps the single
/// <c>RefreshPartiesList()</c> call inside <c>ClanPartiesVM.RefreshValues</c> for
/// <see cref="RefreshPartiesListInRefresh"/>, so the skipped build never runs and no postfix of the list
/// build fires for it. The rewrite is the soft-failing <see cref="TickProfilerTranspiler"/>: a call count other than one
/// leaves the whole method vanilla. <see cref="ClanScreenFastPathService.LogInstall"/> reports the stored count at game
/// init. <see cref="Cleanup"/> zeroes it when the apply fails after the transpiler ran.
/// </summary>
[HarmonyPatch(typeof(ClanPartiesVM), nameof(ClanPartiesVM.RefreshValues))]
[HarmonyPatchCategory(ClanFastOpenCalls.PatchCategory)]
[ClientOnly]
public static class ClanPartiesVM_RefreshValues_ClanFastOpen_Patch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = TickProfilerTranspiler.Rewrite(instructions, BuildSwaps(), "ClanPartiesVM.RefreshValues", null, out var swapped);
        ClanFastOpenCalls.LastSwapped = swapped;
        return result;
    }

    // Harmony registers nothing when the apply fails after the transpiler ran, but the transpiler already stored its count.
    [HarmonyCleanup]
    public static void Cleanup(Exception? ex)
    {
        if (ex != null) ClanFastOpenCalls.LastSwapped = 0;
    }

    /// <summary>The single swap: <c>ClanPartiesVM.RefreshPartiesList()</c> becomes <see cref="RefreshPartiesListInRefresh"/>.</summary>
    internal static IReadOnlyList<CallSwap> BuildSwaps() => new[]
    {
        new CallSwap(AccessTools.Method(typeof(ClanPartiesVM), nameof(ClanPartiesVM.RefreshPartiesList)),
            AccessTools.Method(typeof(ClanPartiesVM_RefreshValues_ClanFastOpen_Patch), nameof(RefreshPartiesListInRefresh))),
    };

    /// <summary>
    /// Stands where <c>RefreshValues</c> called <c>vm.RefreshPartiesList()</c>. With the fast path on the lists the
    /// constructor's build left are still current, so only the two headers are rendered (the tail of
    /// ClanPartiesVM.cs:500-503, which <c>RefreshValues</c> has just reset with whatever {CURRENT} was last set to, not bare text); otherwise vanilla's build runs.
    /// </summary>
    public static void RefreshPartiesListInRefresh(ClanPartiesVM vm)
    {
        if (ClanFastOpenCalls.Service?.ShouldSkipListBuild() == true && TryRenderHeaders(vm)) return;
        vm.RefreshPartiesList();
    }

    // The Refuge postfix (Patch75) appended its rows to Garrisons in the earlier build, so the count here includes them, as
    // vanilla's did. A header that cannot render falls back to the full build, which sets both.
    private static bool TryRenderHeaders(ClanPartiesVM vm)
    {
        try
        {
            GameTexts.SetVariable("CURRENT", vm.Caravans.Count);
            vm.CaravansText = GameTexts.FindText("str_clan_caravans").ToString();
            GameTexts.SetVariable("CURRENT", vm.Garrisons.Count);
            vm.GarrisonsText = GameTexts.FindText("str_clan_garrisons").ToString();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
