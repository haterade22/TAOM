using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.QuickActions.Hooks;

/// <summary>
/// Postfix on <c>SPInventoryVM.OnFinalize</c>. Clears the active-VM reference held by
/// <see cref="InventoryVMAdapter"/> so post-close calls to <c>IsAvailable</c> don't
/// return <c>true</c> on a disposed VM.
///
/// Deep-review #36 caught this lifecycle gap. Symptom-free in QuickActions today
/// (all callers run synchronously while the inventory is open), but EquipPresets
/// (feature #6) will call adapter methods asynchronously and would hit a stale VM.
/// </summary>
[HarmonyPatch(typeof(SPInventoryVM), nameof(SPInventoryVM.OnFinalize))]
[HarmonyPatchCategory("Patch34_QuickActions")]
public static class Patch34_SPInventoryVMFinalize
{
    [HarmonyPostfix]
    public static void Postfix(SPInventoryVM __instance)
    {
        try
        {
            var adapter = IoC.Resolve<IInventoryVMAdapter>() as InventoryVMAdapter;
            // Only clear if THIS instance is still the active one — defensive against
            // a future overlap where a new VM is constructed before the old one finalizes.
            adapter?.ClearActiveIfMatches(__instance);
        }
        catch (Exception ex)
        {
            try { IoC.Resolve<IModLogger>().LogError($"[QuickActions] VM finalize failed: {ex.Message}"); }
            catch { }
        }
    }
}
