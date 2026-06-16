using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

public interface IOnPartyUpgraderUpgradeReadyTroops
{
    void OnUpgradeReadyTroops(PartyBase party);
}
