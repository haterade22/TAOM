using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomRaidModel : DefaultRaidModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly ICareerPassiveService _careerPassives;

    public TaomRaidModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
    {
        _feats = feats;
        _careerPassives = careerPassives;
    }

    public override ExplainedNumber CalculateHitDamage(
        MapEventSide attackerSide, float settlementHitPoints)
    {
        var result = base.CalculateHitDamage(attackerSide, settlementHitPoints);
        _feats.ApplyRaidDamageFeats(
            CultureFeatAdapter.FromOrNull(attackerSide?.LeaderParty?.Owner?.Culture),
            ref result);
        _careerPassives.ApplyFactor(attackerSide?.LeaderParty?.Owner?.StringId, ref result, PassiveEffectType.TroopDamage);
        return result;
    }
}
