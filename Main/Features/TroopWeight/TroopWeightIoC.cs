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
        container.Register<IOnRecruitmentVMRefreshPartyProperties, RecruitmentVMRefreshPartyPropertiesHook>(Reuse.Singleton);
        container.Register<IOnPartyVMPopulatePartyListLabel, PartyVMPopulatePartyListLabelHook>(Reuse.Singleton);
    }

    public static void InitializeHooks(
        IOnPartyBaseNumberOfAllMembers allMembersHook,
        IOnPartyBaseNumberOfRegularMembers regularMembersHook,
        IOnRecruitmentVMRefreshPartyProperties recruitmentVMHook,
        IOnPartyVMPopulatePartyListLabel partyVMHook)
    {
        PartyBase_NumberOfAllMembers_Patch.Initialize(allMembersHook);
        PartyBase_NumberOfRegularMembers_Patch.Initialize(regularMembersHook);
        RecruitmentVM_RefreshPartyProperties_Patch.Initialize(recruitmentVMHook);
        PartyVM_PopulatePartyListLabel_Patch.Initialize(partyVMHook);
    }
}
