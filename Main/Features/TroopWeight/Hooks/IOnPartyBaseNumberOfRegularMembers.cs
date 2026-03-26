using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

public interface IOnPartyBaseNumberOfRegularMembers
{
    void OnPartyBaseNumberOfRegularMembers(PartyBase partyBase, ref int __result);
}
