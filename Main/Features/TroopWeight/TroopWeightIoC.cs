using DryIoc;
using TAOM.Features.TroopWeight.Hooks;

namespace TAOM.Features.TroopWeight;

public static class TroopWeightIoC
{
    public static void RegisterTroopWeightFeature(IContainer container)
    {
        container.Register<ITroopWeightXmlLoader, TroopWeightXmlLoader>(Reuse.Singleton);
        container.Register<ITroopWeightService, TroopWeightService>(Reuse.Singleton);

        container.Register<IOnPartyBaseNumberOfAllMembers, PartyBaseNumberOfAllMembersHook>(Reuse.Singleton);
        container.Register<IOnPartyBaseNumberOfRegularMembers, PartyBaseNumberOfRegularMembersHook>(Reuse.Singleton);
        container.Register<IOnTroopRosterTotalManCount, TroopRosterTotalManCountHook>(Reuse.Singleton);
        container.Register<IOnTroopRosterTotalHealthyCount, TroopRosterTotalHealthyCountHook>(Reuse.Singleton);
        container.Register<IOnRecruitmentVMRefreshPartyProperties, RecruitmentVMRefreshPartyPropertiesHook>(Reuse.Singleton);
        container.Register<IOnPartyVMPopulatePartyListLabel, PartyVMPopulatePartyListLabelHook>(Reuse.Singleton);
    }

    public static void InitializeHooks(
        IOnPartyBaseNumberOfAllMembers allMembersHook,
        IOnPartyBaseNumberOfRegularMembers regularMembersHook,
        IOnTroopRosterTotalManCount totalManCountHook,
        IOnTroopRosterTotalHealthyCount totalHealthyCountHook,
        IOnRecruitmentVMRefreshPartyProperties recruitmentVMHook,
        IOnPartyVMPopulatePartyListLabel partyVMHook)
    {
        PartyBase_NumberOfAllMembers_Patch.Initialize(allMembersHook);
        PartyBase_NumberOfRegularMembers_Patch.Initialize(regularMembersHook);
        TroopRoster_TotalManCount_Patch.Initialize(totalManCountHook);
        TroopRoster_TotalHealthyCount_Patch.Initialize(totalHealthyCountHook);
        RecruitmentVM_RefreshPartyProperties_Patch.Initialize(recruitmentVMHook);
        PartyVM_PopulatePartyListLabel_Patch.Initialize(partyVMHook);
    }
}
