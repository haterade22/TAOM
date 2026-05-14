using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomPartySizeModel : DefaultPartySizeLimitModel
{
    private readonly ICareerPassiveService _careerPassives;
    private static TextObject? _cultureText;
    private static TextObject CultureText => _cultureText ??= GameTexts.FindText("str_culture");

    public TaomPartySizeModel(ICareerPassiveService careerPassives)
    {
        _careerPassives = careerPassives;
    }

    public override ExplainedNumber GetPartyMemberSizeLimit(
        PartyBase party, bool includeDescriptions = false)
    {
        var result = base.GetPartyMemberSizeLimit(party, includeDescriptions);

        var culture = party.Owner?.Culture ?? party.Culture;
        if (culture == null)
            return result;

        if (culture.HasFeat(TaomCulturalFeats.MordorPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.MordorPartySizeFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.GundabadPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.GundabadPartySizeFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.DolGuldurPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.DolGuldurPartySizeFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.IsengardPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.IsengardPartySizeFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.GondorPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.GondorPartySizeFeat.EffectBonus, CultureText);

        _careerPassives.ApplyFactor((party.Owner ?? party.LeaderHero)?.StringId, ref result, PassiveEffectType.PartySize);
        return result;
    }
}
