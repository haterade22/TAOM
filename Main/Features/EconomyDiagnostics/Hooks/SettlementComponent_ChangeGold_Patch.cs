using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.EconomyDiagnostics.Hooks;

/// <summary>
/// The single recorder. <c>SettlementComponent.Gold</c> has a private setter and this is its only
/// mutator, so one patch here observes 100% of town market-gold movement — no flow site can be
/// missed, which is the property that makes the resulting ledger trustworthy enough to act on.
///
/// Prefix + postfix rather than reading the argument: <c>ChangeGold</c> hard-floors the pool at zero
/// (<c>if (Gold &lt; 0) Gold = 0;</c>), so a payment larger than the town's balance is silently
/// truncated. Recording the requested amount would overstate the drain and hide that clamp, which is
/// itself a finding worth seeing.
/// </summary>
[HarmonyPatch(typeof(SettlementComponent), nameof(SettlementComponent.ChangeGold))]
[HarmonyPatchCategory("Patch68_EconomyDiagnostics")]
public static class SettlementComponent_ChangeGold_Patch
{
    private static ITownGoldLedger _ledger;

    /// <summary>
    /// Clears the lazily-cached ledger so a module reload inside the same process does not leave the
    /// recorder writing into an orphaned singleton from the previous container while the console
    /// command reads the new one — the report would sit empty while gold moved. Called from
    /// <c>SubModule.OnSubModuleUnloaded</c>, alongside the existing CrashReport reset.
    /// </summary>
    public static void ResetForUnload() => _ledger = null;

    [HarmonyPrefix]
    public static void Prefix(SettlementComponent __instance, out int __state)
    {
        __state = 0;

        try
        {
            if (__instance != null) __state = __instance.Gold;
        }
        catch (Exception)
        {
            // A half-constructed component during world init — nothing to record.
        }
    }

    [HarmonyPostfix]
    public static void Postfix(SettlementComponent __instance, int __state)
    {
        if (__instance == null) return;

        try
        {
            // Towns only, and the check comes FIRST: only towns receive the daily mint and hold the
            // market pool under investigation (the engine iterates `Town.AllTowns`, so castles are
            // excluded too). Villages touch their pool rarely and get clamped back to 1000 daily.
            // Cheap as this is, `ChangeGold` fires for every settlement, so the early-out belongs
            // above the resolve rather than below it.
            if (!__instance.IsTown) return;

            _ledger ??= IoC.Resolve<ITownGoldLedger>();

            var settlement = __instance.Settlement;
            if (settlement == null) return;

            _ledger.Record(settlement.StringId, TownGoldFlowScope.Current, __instance.Gold - __state);
        }
        catch (Exception)
        {
            // A diagnostic must never be able to break the economy it is measuring.
        }
    }
}
