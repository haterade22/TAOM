using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace TAOM.Features.Diplomacy.Models;

public class TaomKingdomDecisionPermissionModel : DefaultKingdomDecisionPermissionModel
{
    private readonly IDiplomacyService _diplomacyService;

    public TaomKingdomDecisionPermissionModel(IDiplomacyService diplomacyService)
    {
        _diplomacyService = diplomacyService;
    }

    public override bool IsStartAllianceDecisionAllowedBetweenKingdoms(
        Kingdom kingdom1, Kingdom kingdom2, out TextObject reason)
    {
        if (!_diplomacyService.IsAllianceAllowed(kingdom1.StringId, kingdom2.StringId))
        {
            reason = new TextObject("{=taom_alliance_blocked}These kingdoms can never be allied.");
            return false;
        }

        reason = null;
        return true;
    }

    public override bool IsWarDecisionAllowedBetweenKingdoms(
        Kingdom kingdom1, Kingdom kingdom2, out TextObject reason)
    {
        if (_diplomacyService.GetRelationshipTier(kingdom1.StringId, kingdom2.StringId)
            == AllianceTier.Permanent)
        {
            reason = new TextObject("{=taom_war_blocked}These kingdoms are bound by an unbreakable alliance.");
            return false;
        }

        return base.IsWarDecisionAllowedBetweenKingdoms(kingdom1, kingdom2, out reason);
    }
}
