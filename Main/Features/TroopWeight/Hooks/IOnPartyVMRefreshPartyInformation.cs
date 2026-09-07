using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace TAOM.Features.TroopWeight.Hooks;

/// Party-screen troop header ("Troops (N / M)"). Seams on the CALLER of PartyVM.PopulatePartyListLabel
/// rather than the label builder itself: that builder is `private static`, receives no party, and is
/// shared with the PRISONER headers — patching it would weight prisoner counts too.
public interface IOnPartyVMRefreshPartyInformation
{
    void OnRefreshPartyInformation(PartyVM partyVm);
}
