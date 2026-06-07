using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace TAOM.Features.TroopWeight.Hooks;

public interface IOnCampaignUIHelperGetPartyHealthTooltip
{
    void OnGetPartyHealthTooltip(PartyBase party, ref List<TooltipProperty> __result);
}
