using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.TroopProgression;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomPartyTroopUpgradeModel : DefaultPartyTroopUpgradeModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly ICareerPassiveService _careerPassives;
    private readonly ITroopCostService _troopCost;

    public TaomPartyTroopUpgradeModel(
        ICulturalFeatsService feats, ICareerPassiveService careerPassives, ITroopCostService troopCost)
    {
        _feats = feats;
        _careerPassives = careerPassives;
        _troopCost = troopCost;
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

    // Crash bundle a7dc3a20: vanilla returns 0 whenever the upgrade target does not reach a
    // higher tier bracket, and CampaignUIHelper.GetTroopXPTooltip divides by that. TAOM ships
    // ten deliberate same-level lateral edges, so the service prices them rather than the data
    // forbidding them. Base's 100000000 "not a real target" sentinel is positive and survives.
    public override int GetXpCostForUpgrade(
        PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
        => _troopCost.GetUpgradeXpCost(
            base.GetXpCostForUpgrade(party, characterObject, upgradeTarget),
            upgradeTarget?.Level ?? 0);
}
