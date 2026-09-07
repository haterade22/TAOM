using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace TAOM.Features.TroopWeight.Hooks;

/// The "Land Troop Capacity" row of the MAIN-party health tooltip. Deliberately scoped to that ONE row:
/// the sibling "Battle Ready Troops" / "Wounded Troops" rows are HEADCOUNTS and must keep reading raw.
/// Weighting them is what manufactured the phantom-wounded bug (RCA 2026-06-07).
///
/// Takes a PartyBase even though only the main party reaches it today: CampaignUIHelper's any-party
/// sibling (GetPartyHealthTooltip) builds no capacity row and has no caller in the v1.4.8 client, so
/// patching it was dead code and was removed. The parameter stays so the hook can serve a real any-party
/// surface if one ever appears, without reshaping the interface.
public interface IOnCampaignUIHelperGetPartyHealthTooltip
{
    void OnGetPartyHealthTooltip(PartyBase party, List<TooltipProperty> properties);
}
