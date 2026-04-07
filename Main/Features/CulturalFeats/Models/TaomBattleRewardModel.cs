using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomBattleRewardModel : DefaultBattleRewardModel
{
    private static TextObject? _cultureText;
    private static TextObject CultureText => _cultureText ??= GameTexts.FindText("str_culture");

    public override ExplainedNumber CalculateRenownGain(
        PartyBase party, float renownValueOfBattle, float contributionShare)
    {
        var result = base.CalculateRenownGain(party, renownValueOfBattle, contributionShare);

        var culture = party.Owner?.Culture ?? party.Culture;
        if (culture?.HasFeat(TaomCulturalFeats.UmbarRenownFeat) == true)
            result.AddFactor(TaomCulturalFeats.UmbarRenownFeat.EffectBonus, CultureText);

        var hero = party.Owner ?? party.LeaderHero;
        if (hero != null)
            CareerPassiveHelper.ApplyFactor(hero, ref result, PassiveEffectType.BattleRenownGain);

        return result;
    }
}
