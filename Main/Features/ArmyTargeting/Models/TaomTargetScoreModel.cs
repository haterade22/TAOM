using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.ArmyTargeting.Models;

public class TaomTargetScoreModel : DefaultTargetScoreCalculatingModel
{
    private readonly IArmyTargetingService _service;

    public TaomTargetScoreModel(IArmyTargetingService service)
    {
        _service = service;
    }

    public override float GetTargetScoreForFaction(
        Settlement targetSettlement, Army.ArmyTypes missionType,
        MobileParty mobileParty, float ourStrength)
    {
        // Extract primitives at boundary — no sealed types cross into service
        // Use faction StringId (empire_s/empire_w/empire) not culture StringId — Mordor, Gondor,
        // and Dunland all share Culture.empire so culture cannot distinguish them.
        string factionId = mobileParty.MapFaction?.StringId;

        float effectiveStrength = missionType == Army.ArmyTypes.Besieger
            ? ourStrength * _service.GetStrengthMultiplier(factionId)
            : ourStrength;

        float baseScore = base.GetTargetScoreForFaction(targetSettlement, missionType, mobileParty, effectiveStrength);

        // Only Besieger armies: Raider re-selects freely, Defender stays reactive
        if (baseScore <= 0f || missionType != Army.ArmyTypes.Besieger)
            return baseScore;

        string committedTargetId = (mobileParty.Army?.AiBehaviorObject as Settlement)?.StringId;

        float targetMultiplier = _service.GetTargetMultiplier(targetSettlement.StringId, committedTargetId, factionId);
        float distanceCompensation = _service.GetDistanceCompensation(factionId, targetSettlement.StringId);
        return baseScore * targetMultiplier * distanceCompensation;
    }
}
