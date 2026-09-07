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

        container.Register<IOnPartyUpgraderUpgradeReadyTroops, PartyUpgraderUpgradeReadyTroopsHook>(Reuse.Singleton);

        // Display side (2026-09-06 usage-frame reframe): the elite tax is still ENFORCED by deflating the
        // party-size limit in TaomPartySizeModel, but every capacity readout now shows weighted-used over
        // the true base ("19 / 20") instead of raw over the deflated limit ("10 / 11"), because a shrinking
        // denominator reads as "adding troops made my party smaller". RegisterMany so all five surfaces
        // share ONE singleton — they read the same per-party caches on the service.
        container.RegisterMany<TroopWeightDisplayHook>(Reuse.Singleton);
    }

    public static void InitializeHooks(
        IOnPartyUpgraderUpgradeReadyTroops upgraderHook,
        IOnPartyVMRefreshPartyInformation partyScreenHook,
        IOnClanPartyItemUpdateProperties clanScreenHook,
        IOnRecruitmentVMRefreshPartyProperties recruitmentHook,
        IOnCampaignUIHelperGetPartyHealthTooltip healthTooltipHook,
        IOnPartyCharacterVMRefreshValues rowTagHook)
    {
        PartyUpgraderUpgradeReadyTroops_Patch.Initialize(upgraderHook);
        PartyVM_RefreshPartyInformation_Patch.Initialize(partyScreenHook);
        ClanPartyItemVM_UpdateProperties_Patch.Initialize(clanScreenHook);
        RecruitmentVM_RefreshPartyProperties_Patch.Initialize(recruitmentHook);
        // Only the MAIN-party health tooltip. Its any-party sibling, CampaignUIHelper.GetPartyHealthTooltip,
        // was patched here until a v1.4.8 decompile showed it never emits a "Land Troop Capacity" row at all
        // (that row exists only in the parameterless main-party builder) and has no caller in any shipped
        // client assembly — so the patch was searching for a label that could not be there.
        CampaignUIHelper_GetMainPartyHealthTooltip_Patch.Initialize(healthTooltipHook);
        PartyCharacterVM_RefreshValues_Patch.Initialize(rowTagHook);
    }
}
