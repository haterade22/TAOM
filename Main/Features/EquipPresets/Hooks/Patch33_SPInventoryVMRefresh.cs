using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.EquipPresets.Hooks;

/// <summary>
/// Postfix on <c>SPInventoryVM.RefreshValues</c>. Captures the live VM into
/// <see cref="InventoryScreenAdapter"/> so the preset menu can read active-hero / equipment-mode
/// state and trigger a refresh after Load. We hook <c>RefreshValues</c> (not constructor) because
/// the VM is constructed before <c>_currentCharacter</c> is finalized; <c>RefreshValues</c> is
/// guaranteed to fire with the final state.
///
/// Patch number 33 — verified free at port time (CLAUDE.md table tops out at Patch30; QuickActions
/// took 34, FiefManagement took 36; Patch31 = SmartCavalryAI).
///
/// Performance: <c>RefreshValues</c> fires often while the inventory screen is open (every
/// transaction tick). Adapter + logger references are lazy-cached statically per
/// <c>.claude/rules/harmony-patches.md</c> "Reflection in hot paths" / "IoC.Resolve in Hot Paths"
/// guidance. The static cache survives across screens; the resolved singletons themselves are
/// process-lived so caching is safe.
/// </summary>
[HarmonyPatch(typeof(SPInventoryVM), nameof(SPInventoryVM.RefreshValues))]
[HarmonyPatchCategory("Patch33_EquipPresets")]
public static class Patch33_SPInventoryVMRefresh
{
    // Phase 9b #141 P1 — use the interface type, not the concrete. SetActive is now on
    // IInventoryScreenAdapter so this works for any implementation (incl. test mocks). Pre-fix
    // `as InventoryScreenAdapter` would return null for any non-concrete impl and silently skip
    // the call without any log signal.
    private static IInventoryScreenAdapter? _cachedAdapter;
    private static IModLogger? _cachedLogger;

    [HarmonyPostfix]
    public static void Postfix(SPInventoryVM __instance)
    {
        try
        {
            _cachedAdapter ??= IoC.Resolve<IInventoryScreenAdapter>();
            _cachedAdapter?.SetActive(__instance);
        }
        catch (Exception ex)
        {
            try
            {
                _cachedLogger ??= IoC.Resolve<IModLogger>();
                _cachedLogger?.LogError($"[EquipPresets] VM capture failed: {ex.Message}");
            }
            catch { /* logger unresolvable — nothing to do */ }
        }
    }
}
