using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.ArmyTargeting.Models;

public class TaomTargetScoreModel : DefaultTargetScoreCalculatingModel
{
    private readonly IArmyTargetingService _service;
    private readonly ITargetScoreContextFactory _contextFactory;

    public TaomTargetScoreModel(IArmyTargetingService service, ITargetScoreContextFactory contextFactory)
    {
        _service = service;
        _contextFactory = contextFactory;
    }

    public override float GetTargetScoreForFaction(
        Settlement targetSettlement, Army.ArmyTypes missionType,
        MobileParty mobileParty, float ourStrength)
    {
        // gamemodels.md rule 4: boundary conversion plus a direct delegate, no branching. All the
        // sealed-type extraction, the mission mapping and the decision about whether reach needs
        // measuring live in ITargetScoreContextFactory.
        //
        // Faction identity comes from MapFaction.StringId, never Culture.StringId: empire_s
        // (Mordor), empire_w (GONDOR) and empire (Dunland) all share culture "empire", so culture
        // cannot tell them apart.
        var context = _contextFactory.Create(targetSettlement, missionType, mobileParty, ourStrength);
        context.BaseScore = base.GetTargetScoreForFaction(targetSettlement, missionType, mobileParty, context.EffectiveStrength);
        return _service.ApplyTargetScoreModifiers(context);
    }
}
