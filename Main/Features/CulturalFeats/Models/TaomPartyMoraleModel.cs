using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomPartyMoraleModel : DefaultPartyMoraleModel
{
    private static TextObject CultureText => GameTexts.FindText("str_culture");

    public override ExplainedNumber GetEffectivePartyMorale(
        MobileParty party, bool includeDescription = false)
    {
        var result = base.GetEffectivePartyMorale(party, includeDescription);

        var culture = party.Party?.Owner?.Culture;
        if (culture == null)
            return result;

        if (culture.HasFeat(TaomCulturalFeats.GondorMoraleFeat))
            result.Add(TaomCulturalFeats.GondorMoraleFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.RohanMoraleFeat))
            result.Add(TaomCulturalFeats.RohanMoraleFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.EreborMoraleFeat))
            result.Add(TaomCulturalFeats.EreborMoraleFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.MirkwoodMoraleFeat))
            result.Add(TaomCulturalFeats.MirkwoodMoraleFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.LothlorienMoraleFeat))
            result.Add(TaomCulturalFeats.LothlorienMoraleFeat.EffectBonus, CultureText);

        return result;
    }
}
