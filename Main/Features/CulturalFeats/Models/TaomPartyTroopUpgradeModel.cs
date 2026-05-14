using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomPartyTroopUpgradeModel : DefaultPartyTroopUpgradeModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly ICareerPassiveService _careerPassives;

    public TaomPartyTroopUpgradeModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
    {
        _feats = feats;
        _careerPassives = careerPassives;
    }

    public override ExplainedNumber GetGoldCostForUpgrade(
        PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
    {
        var result = base.GetGoldCostForUpgrade(party, characterObject, upgradeTarget);
        _feats.ApplyTroopUpgradeFeats(
            CultureFeatAdapter.FromOrNull(party.Owner?.Culture ?? party.Culture),
            characterObject.IsMounted,
            ref result);
        _careerPassives.ApplyFactor((party.Owner ?? party.LeaderHero)?.StringId, ref result, PassiveEffectType.TroopUpgradeCost);
        return result;
    }
}
