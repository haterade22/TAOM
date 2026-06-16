using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace TAOM.Features.Diplomacy.Models;

public class TaomKingdomDecisionPermissionModel : DefaultKingdomDecisionPermissionModel
{
    private readonly IDiplomacyService _diplomacyService;
    private readonly IWarOfTheRingService _wotrService;

    public TaomKingdomDecisionPermissionModel(IDiplomacyService diplomacyService, IWarOfTheRingService wotrService)
    {
        _diplomacyService = diplomacyService;
        _wotrService = wotrService;
    }

    public override bool IsStartAllianceDecisionAllowedBetweenKingdoms(
        Kingdom kingdom1, Kingdom kingdom2, out TextObject reason)
    {
        bool involvesPlayer = PlayerKingdomHelper.InvolvesPlayerRuledKingdom(kingdom1, kingdom2);

        if (!_diplomacyService.IsAllianceDecisionAllowed(kingdom1.StringId, kingdom2.StringId, involvesPlayer))
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
        if (!_diplomacyService.IsWarAllowed(kingdom1.StringId, kingdom2.StringId))
        {
            reason = new TextObject("{=taom_war_blocked}These kingdoms are bound by an unbreakable alliance.");
            return false;
        }

        return base.IsWarDecisionAllowedBetweenKingdoms(kingdom1, kingdom2, out reason);
    }

    public override bool IsPeaceDecisionAllowedBetweenKingdoms(
        Kingdom kingdom1, Kingdom kingdom2, out TextObject reason)
    {
        if (_wotrService.ShouldBlockPeace(kingdom1.StringId, kingdom2.StringId))
        {
            reason = new TextObject("{=taom_wotr_no_peace}The War of the Ring rages. There can be no peace.");
            return false;
        }

        return base.IsPeaceDecisionAllowedBetweenKingdoms(kingdom1, kingdom2, out reason);
    }
}
