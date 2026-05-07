using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TAOM.Core.Logging;

namespace TAOM.Features.QuickActions.Hooks;

/// <summary>
/// Prefix on <c>SPInventoryVM.ExecuteSellAllItems</c>. When QuickActions is enabled,
/// returns <c>false</c> to skip vanilla and opens the multi-action inquiry. When the
/// feature is disabled or any setup step fails, returns <c>true</c> so vanilla bulk-sell
/// runs and the player isn't stranded.
///
/// Codex review #36 caught an earlier version that hand-rolled "Sell All (Vanilla)" via
/// a manual ProcessSellItem loop, which dropped capacity-budget, settlement-gold, full-stack,
/// and zero-count-cleanup logic vanilla applies in <c>TransferAll(false)</c>. The fix:
/// a thread-static bypass flag lets the menu re-enter <c>__instance.ExecuteSellAllItems()</c>
/// while suppressing this Prefix, so vanilla runs unmodified.
/// </summary>
[HarmonyPatch(typeof(SPInventoryVM), nameof(SPInventoryVM.ExecuteSellAllItems))]
[HarmonyPatchCategory("Patch34_QuickActions")]
public static class Patch34_SellAllItemsMenu
{
    [ThreadStatic]
    private static bool _bypassQuickActions;

    /// <summary>
    /// Sets the bypass flag so the next call to <c>SPInventoryVM.ExecuteSellAllItems()</c>
    /// runs vanilla logic unmodified. Used by the "Sell All (Vanilla)" menu option.
    /// </summary>
    public static void RunVanillaSellAll(SPInventoryVM vm)
    {
        if (vm == null) return;
        _bypassQuickActions = true;
        try
        {
            vm.ExecuteSellAllItems();
        }
        finally
        {
            _bypassQuickActions = false;
        }
    }

    [HarmonyPrefix]
    public static bool Prefix(SPInventoryVM __instance)
    {
        // Re-entry from RunVanillaSellAll: let vanilla run.
        if (_bypassQuickActions) return true;

        try
        {
            var settings = IoC.Resolve<IQuickActionsSettingsProvider>();
            if (!settings.EnableQuickActions) return true;

            var service = IoC.Resolve<IQuickActionsService>();

            // The "Sell All (Vanilla)" callback re-enters ExecuteSellAllItems with the bypass
            // flag set, which gives the player the actual vanilla behavior (TransferAll
            // including capacity/settlement/full-stack/sort/zero-count-cleanup logic).
            Action vanilla = () => RunVanillaSellAll(__instance);

            service.OpenMenu(originalSellAll: vanilla);
            return false;
        }
        catch (Exception ex)
        {
            try { IoC.Resolve<IModLogger>().LogError($"[QuickActions] Patch34 fault: {ex.Message}"); }
            catch { }
            return true; // fall back to vanilla
        }
    }
}
