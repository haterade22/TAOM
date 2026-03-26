using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

public interface IOnPartyBaseNumberOfAllMembers
{
    void OnPartyBaseNumberOfAllMembers(PartyBase partyBase, ref int __result);
}
