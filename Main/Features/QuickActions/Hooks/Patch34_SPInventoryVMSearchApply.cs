using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TAOM.Core.Logging;

namespace TAOM.Features.QuickActions.Hooks;

/// <summary>
/// Postfix on <c>SPInventoryVM.RefreshCallbacks</c>. Fires once per inventory open
/// (called by the inventory screen's <c>OnActivate</c>), at which point the VM is
/// bound to the UI and ready for property writes.
/// Reads <c>InventorySearchCampaignBehavior.IsSearchAvailable</c> and applies it.
/// </summary>
[HarmonyPatch(typeof(SPInventoryVM), nameof(SPInventoryVM.RefreshCallbacks))]
[HarmonyPatchCategory("Patch34_QuickActions")]
public static class Patch34_SPInventoryVMSearchApply
{
    [HarmonyPostfix]
    public static void Postfix(SPInventoryVM __instance)
    {
        try
        {
            var behavior = IoC.Resolve<InventorySearchCampaignBehavior>();
            __instance.IsSearchAvailable = behavior?.IsSearchAvailable ?? true;
        }
        catch (Exception ex)
        {
            try { IoC.Resolve<IModLogger>().LogError($"[QuickActions] search-apply failed: {ex.Message}"); }
            catch { }
        }
    }
}
