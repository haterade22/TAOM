// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), nameplate-cull.
using HarmonyLib;
using SandBox.ViewModelCollection.Nameplate;

namespace TAOM.Features.NameplateCull.Hooks;

/// <summary>
/// Patch104 (docs/reference/harmony-patch-registry.md "Patch104_NameplateCull"). A bool prefix on the campaign map's
/// once-per-frame nameplate update: it hands the frame to <see cref="INameplateCullService"/>, which runs the vanilla
/// update with the plates that are hidden and stay hidden left out, and returns false so the engine's own loop does not
/// run a second time. Every other outcome returns true and vanilla runs, whole: the toggle off, the cull unavailable,
/// an error. Main thread, once per map frame.
/// </summary>
[HarmonyPatch(typeof(SettlementNameplatesVM), nameof(SettlementNameplatesVM.Update))]
[HarmonyPatchCategory(NameplateCullModule.PatchCategory)]
public static class SettlementNameplatesVM_Update_NameplateCull_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(SettlementNameplatesVM __instance)
    {
        var service = NameplateCullCalls.Service;
        if (service == null) return true;
        try { return !service.TryUpdate(__instance); }
        catch { return true; }
    }
}
