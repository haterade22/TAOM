using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Library;

namespace TAOM.Features.TroopWeight.Hooks;

public interface IOnPartyVMPopulatePartyListLabel
{
    bool OnPartyVMPopulatePartyListLabel(
        ref string __result,
        MBBindingList<PartyCharacterVM> partyList,
        int limit);
}
