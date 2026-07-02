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

        // TEMPORARY: troop-count diagnostic behavior (special-currency undercount investigation).
        // Remove alongside TroopCountDiagnosticsBehavior once the root cause is pinned.
        container.Register<TroopCountDiagnosticsBehavior>(Reuse.Singleton);

        container.Register<IOnPartyBaseNumberOfAllMembers, PartyBaseNumberOfAllMembersHook>(Reuse.Singleton);
        container.Register<IOnPartyBaseNumberOfRegularMembers, PartyBaseNumberOfRegularMembersHook>(Reuse.Singleton);
        container.Register<IOnRecruitmentVMRefreshPartyProperties, RecruitmentVMRefreshPartyPropertiesHook>(Reuse.Singleton);
        container.Register<IOnPartyVMPopulatePartyListLabel, PartyVMPopulatePartyListLabelHook>(Reuse.Singleton);
        container.Register<IOnPartyUpgraderUpgradeReadyTroops, PartyUpgraderUpgradeReadyTroopsHook>(Reuse.Singleton);

        // One instance fixes all four phantom-wounded display surfaces (it implements four IOn* hooks).
        container.Register<TroopWeightDisplayHook>(Reuse.Singleton);
    }

    public static void InitializeHooks(
        IOnPartyBaseNumberOfAllMembers allMembersHook,
        IOnPartyBaseNumberOfRegularMembers regularMembersHook,
        IOnRecruitmentVMRefreshPartyProperties recruitmentVMHook,
        IOnPartyVMPopulatePartyListLabel partyVMHook,
        IOnPartyUpgraderUpgradeReadyTroops upgraderHook,
        TroopWeightDisplayHook displayHook)
    {
        PartyBase_NumberOfAllMembers_Patch.Initialize(allMembersHook);
        PartyBase_NumberOfRegularMembers_Patch.Initialize(regularMembersHook);
        RecruitmentVM_RefreshPartyProperties_Patch.Initialize(recruitmentVMHook);
        PartyVM_PopulatePartyListLabel_Patch.Initialize(partyVMHook);
        PartyUpgraderUpgradeReadyTroops_Patch.Initialize(upgraderHook);

        // Phantom-wounded display fixes (battle-ready / wounded derived from weighted vs unweighted getters).
        CampaignUIHelper_GetMainPartyHealthTooltip_Patch.Initialize(displayHook);
        CampaignUIHelper_GetPartyHealthTooltip_Patch.Initialize(displayHook);
        GameMenuPartyItemVM_RefreshCounts_Patch.Initialize(displayHook);
        PartyBaseHelper_GetPartySizeText_Patch.Initialize(displayHook);
        ClanPartyItemVM_UpdateProperties_Patch.Initialize(displayHook);
    }
}
