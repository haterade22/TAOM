using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;

namespace TAOM.Features.ShadowfaxScreens.Clan.Hooks;

/// <summary>
/// Clan fast open (docs/reference/harmony-patch-registry.md "Patch127_ShadowfaxClan"). A void prefix and finalizer that only
/// time the builds that REALLY run: the skipped call never reaches this method (the <c>RefreshValues</c> transpiler took it
/// out of the call site), so every execution here is a build, and its start stamp rides in <c>__state</c> (0 means "not
/// timed"). The Refuge postfix on this method therefore runs only for a real build. Void
/// finalizer, <c>First</c>.
/// </summary>
[HarmonyPatch(typeof(ClanPartiesVM), nameof(ClanPartiesVM.RefreshPartiesList))]
[HarmonyPatchCategory(ClanFastOpenCalls.PatchCategory)]
[ClientOnly]
public static class ClanPartiesVM_RefreshPartiesList_ClanFastOpen_Patch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(out long __state)
    {
        __state = 0;
        try { __state = ClanFastOpenCalls.Service?.BeginBuild() ?? 0; }
        catch { /* a count must never change the engine's outcome */ }
    }

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(long __state)
    {
        try { ClanFastOpenCalls.Service?.EndBuild(__state); }
        catch { /* a count must never change the engine's outcome */ }
    }
}
