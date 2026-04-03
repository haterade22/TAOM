using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomRaidModel : DefaultRaidModel
{
    private static TextObject CultureText => GameTexts.FindText("str_culture");

    public override ExplainedNumber CalculateHitDamage(
        MapEventSide attackerSide, float settlementHitPoints)
    {
        var result = base.CalculateHitDamage(attackerSide, settlementHitPoints);

        var culture = attackerSide?.LeaderParty?.Owner?.Culture;
        if (culture == null)
            return result;

        if (culture.HasFeat(TaomCulturalFeats.MordorRaidDamageFeat))
            result.AddFactor(TaomCulturalFeats.MordorRaidDamageFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.GundabadRaidDamageFeat))
            result.AddFactor(TaomCulturalFeats.GundabadRaidDamageFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.IsengardRaidDamageFeat))
            result.AddFactor(TaomCulturalFeats.IsengardRaidDamageFeat.EffectBonus, CultureText);

        return result;
    }
}
