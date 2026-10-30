using System;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TAOM.Features.ShadowfaxScreens.Diagnostics;

namespace TAOM.Features.ShadowfaxScreens.Inventory.Hooks;

// Verified on the installed v1.5.5: SPInventoryVM has one constructor, (InventoryLogic inventoryLogic, bool
// isInCivilianModeByDefault, Func<WeaponComponentData, ItemObject.ItemUsageSetFlags> getItemUsageSetFlags). It sets
// the private `_usageType` (the screen's InventoryMode) first and builds every row through InitializeInventory. The
// timer wraps the view-model build only: the movie load that follows in GauntletInventoryScreen.OnReady is not in the
// span. Observe only: the prefix returns nothing, the void finalizer keeps Harmony's rethrow, and `__state` is the
// only link between them (a stripped prefix leaves it null and the finalizer does nothing). The finalizer reads the
// mode and the two side counts from the object being built, so no active-state lookup is needed.

[HarmonyPatch(typeof(SPInventoryVM), MethodType.Constructor, new[]
{
    typeof(InventoryLogic), typeof(bool), typeof(Func<WeaponComponentData, ItemObject.ItemUsageSetFlags>),
})]
[HarmonyPatchCategory(ShadowfaxInventoryCalls.PatchCategory)]
[ClientOnly]
public static class SPInventoryVM_Ctor_ShadowfaxPatch
{
    [HarmonyPrefix]
    public static void Prefix(out ScreenOpenWindow? __state) => __state = ShadowfaxInventoryCalls.BeginOpen();

    [HarmonyFinalizer]
    public static void Finalizer(InventoryLogic? inventoryLogic, InventoryScreenHelper.InventoryMode ____usageType,
        Exception? __exception, ScreenOpenWindow? __state)
    {
        if (__state == null) return;
        ShadowfaxInventoryCalls.EndOpen(__state, __exception, ____usageType.ToString(),
            Count(inventoryLogic, InventoryLogic.InventorySide.OtherInventory),
            Count(inventoryLogic, InventoryLogic.InventorySide.PlayerInventory));
    }

    // A constructor that threw part-way can leave the logic's rosters unset: report n/a rather than mask its exception.
    private static int Count(InventoryLogic? logic, InventoryLogic.InventorySide side)
    {
        try { return logic?.GetElementCountOnSide(side) ?? -1; }
        catch { return -1; }
    }
}
