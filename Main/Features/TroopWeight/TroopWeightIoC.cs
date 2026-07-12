using DryIoc;
using TAOM.Features.TroopWeight.Diagnostics;
using TAOM.Features.TroopWeight.Hooks;

namespace TAOM.Features.TroopWeight;

public static class TroopWeightIoC
{
    public static void RegisterTroopWeightFeature(IContainer container)
    {
        container.Register<ITroopWeightXmlLoader, TroopWeightXmlLoader>(Reuse.Singleton);
        container.Register<ITroopWeightService, TroopWeightService>(Reuse.Singleton);

        // TEMPORARY: special-currency troop-count undercount diagnostic (separate investigation).
        // Remove alongside TroopCountDiagnosticsBehavior once that root cause is pinned.
        container.Register<TroopCountDiagnosticsBehavior>(Reuse.Singleton);

        // Shed-on-upgrade is the ONLY surviving Patch17 hook after the 2026-07-11 rework moved the "elite
        // tax" from inflating the member count to deflating the party-size limit (TaomPartySizeModel). The
        // count-getter + weighted-display patches were removed so every troop count now reads raw.
        container.Register<IOnPartyUpgraderUpgradeReadyTroops, PartyUpgraderUpgradeReadyTroopsHook>(Reuse.Singleton);
    }

    public static void InitializeHooks(IOnPartyUpgraderUpgradeReadyTroops upgraderHook)
    {
        PartyUpgraderUpgradeReadyTroops_Patch.Initialize(upgraderHook);
    }
}
