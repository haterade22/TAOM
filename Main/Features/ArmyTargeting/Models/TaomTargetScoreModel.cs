using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.ArmyTargeting.Models;

public class TaomTargetScoreModel : DefaultTargetScoreCalculatingModel
{
    private readonly IArmyTargetingService _service;
    private readonly IMapReachAdapter _reach;

    public TaomTargetScoreModel(IArmyTargetingService service, IMapReachAdapter reach)
    {
        _service = service;
        _reach = reach;
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
        // (GONDOR) and empire (Dunland) all share culture "empire" so culture cannot distinguish
        // them. This comment previously said empire_w was Rohan, which is wrong: Rohan is vlandia.
        var mission = ArmyMissionMapper.FromArmyType(missionType);
        var attackerFaction = mobileParty?.MapFaction;
        string factionId = attackerFaction?.StringId;

        float effectiveStrength = _service.GetEffectiveStrength(
            factionId, mission == ArmyTargetingMission.Besieger, ourStrength);

        float baseScore = base.GetTargetScoreForFaction(targetSettlement, missionType, mobileParty, effectiveStrength);

        return _service.ApplyTargetScoreModifiers(new TargetScoreContext
        {
            BaseScore = baseScore,
            Mission = mission,
            FactionId = factionId,
            TargetFactionId = targetSettlement?.MapFaction?.StringId,
            TargetSettlementId = targetSettlement?.StringId,
            CommittedTargetId = (mobileParty?.Army?.AiBehaviorObject as Settlement)?.StringId,

            // Measured only for sieges. Raiders are already hard-zeroed past 5 town gaps by
            // vanilla, and a Defender's target is its own fief, so neither reads this. NaN is the
            // adapter's "unmeasurable" value and the service treats it as no suppression.
            NormalizedDistance = mission == ArmyTargetingMission.Besieger
                ? _reach.GetNormalizedDistanceToNearestFortification(targetSettlement, attackerFaction)
                : float.NaN,
        });
    }
}
