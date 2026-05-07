using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.QuickActions.Hooks;

/// <summary>
/// Postfix on <c>SPInventoryVM</c> constructor. Captures the active VM into the
/// adapter so the service + search-toggle can interact with it. Cleared by
/// <see cref="Patch34_SPInventoryVMSearchApply"/> not — the adapter releases the
/// reference when a new VM is constructed (each inventory open creates a new VM).
/// </summary>
[HarmonyPatch(typeof(SPInventoryVM), MethodType.Constructor,
    typeof(TaleWorlds.CampaignSystem.Inventory.InventoryLogic),
    typeof(bool),
    typeof(Func<TaleWorlds.Core.WeaponComponentData, TaleWorlds.Core.ItemObject.ItemUsageSetFlags>))]
[HarmonyPatchCategory("Patch34_QuickActions")]
public static class Patch34_SPInventoryVMCapture
{
    [HarmonyPostfix]
    public static void Postfix(SPInventoryVM __instance)
    {
        try
        {
            var adapter = IoC.Resolve<IInventoryVMAdapter>() as InventoryVMAdapter;
            adapter?.SetActive(__instance);
        }
        catch (Exception ex)
        {
            try { IoC.Resolve<IModLogger>().LogError($"[QuickActions] VM capture failed: {ex.Message}"); }
            catch { }
        }
    }
}
