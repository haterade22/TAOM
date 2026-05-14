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
        // Phase 9b #138 — boundary extraction + pure delegation per gamemodels.md rule 4.
        // The model class is a thin entry point: extract sealed-type primitives once, hand off
        // to IArmyTargetingService for all decision logic.
        //
        // factionId via MapFaction.StringId (not Culture.StringId) — empire_s (Mordor), empire_w
        // (Rohan), and empire (Dunland) all share culture "empire" so culture cannot distinguish
        // them.
        bool isBesieger = missionType == Army.ArmyTypes.Besieger;
        string factionId = mobileParty.MapFaction?.StringId;

        float effectiveStrength = _service.GetEffectiveStrength(factionId, isBesieger, ourStrength);
        float baseScore = base.GetTargetScoreForFaction(targetSettlement, missionType, mobileParty, effectiveStrength);

        string committedTargetId = (mobileParty.Army?.AiBehaviorObject as Settlement)?.StringId;
        return _service.ApplyTargetScoreModifiers(baseScore, isBesieger, factionId, targetSettlement.StringId, committedTargetId);
    }
}
