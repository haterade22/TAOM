using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace TAOM.Features.TroopWeight.Hooks;

public interface IOnPartyBaseHelperGetPartySizeText
{
    void OnGetPartySizeText(PartyBase party, ref TextObject __result);
}
