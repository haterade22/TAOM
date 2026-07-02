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
        // Vanilla PartyBaseHelper.HasFeat precedence via the shared CultureFeatAdapter helper —
        // same fix Codex 43 made to speed model and the 3-pack RCA applied to size model.
        _feats.ApplyTroopUpgradeFeats(
            CultureFeatAdapter.FromOrNull(party),
            characterObject.IsMounted,
            ref result);
        _careerPassives.ApplyFactor(CareerPassiveHero.ResolveId(party), ref result, PassiveEffectType.TroopUpgradeCost);
        return result;
    }
}
