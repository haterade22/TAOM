using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace TAOM.Features.TroopWeight.Hooks;

/// Clan-screen party row: the "X/limit" figure and its "Party Size:" subtitle.
public interface IOnClanPartyItemUpdateProperties
{
    void OnClanPartyItemUpdateProperties(ClanPartyItemVM item);
}
