using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;
using TAOM.Features.MissionPerf;

namespace TAOM.Features.ShadowfaxScreens.Clan;

/// <summary>
/// The service the Clan fast open hooks reach, handed over by the module's static initialisation, and the one helper the
/// <c>RefreshValues</c> transpiler swaps in for vanilla's <c>RefreshPartiesList()</c> call.
/// </summary>
internal static class ClanFastOpenCalls
{
    /// <summary>The Harmony category of the Clan hooks.</summary>
    internal const string PatchCategory = "Patch127_ShadowfaxClan";

    /// <summary>How many sites the transpiler swapped on its last run; 0 until it ran, and 0 when it soft-failed.</summary>
    internal static int LastSwapped { get; set; }

    internal static ClanScreenFastPathService? Service { get; private set; }

    internal static void Initialize(ClanScreenFastPathService? service) => Service = service;

    /// <summary>The single swap: <c>ClanPartiesVM.RefreshPartiesList()</c> becomes <see cref="RefreshPartiesListInRefresh"/>.</summary>
    internal static IReadOnlyList<CallSwap> BuildSwaps() => new[]
    {
        new CallSwap(AccessTools.Method(typeof(ClanPartiesVM), nameof(ClanPartiesVM.RefreshPartiesList)),
            AccessTools.Method(typeof(ClanFastOpenCalls), nameof(RefreshPartiesListInRefresh))),
    };

    /// <summary>
    /// Stands where <c>RefreshValues</c> called <c>vm.RefreshPartiesList()</c>. With the fast path on the lists the
    /// constructor's build left are still current, so only the two headers are rendered (the tail of
    /// ClanPartiesVM.cs:465-468 in v1.4.8, which <c>RefreshValues</c> has just reset with whatever {CURRENT} was last set to, not bare text); otherwise vanilla's build runs.
    /// </summary>
    public static void RefreshPartiesListInRefresh(ClanPartiesVM vm)
    {
        if (Service?.ShouldSkipListBuild() == true && TryRenderHeaders(vm)) return;
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
