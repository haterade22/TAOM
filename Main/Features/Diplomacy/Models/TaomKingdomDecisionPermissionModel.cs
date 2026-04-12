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

    // v1.4.0 compat: DefaultKingdomDecisionPermissionModel.IsPeaceDecisionAllowedBetweenKingdoms
    // was rewritten to check IsAtWarByCallToWarAgreement in both directions (k1→k2 and k2→k1),
    // and that method's signature gained an `out Kingdom callingKingdom` parameter.
    // TAOM is unaffected because:
    //   1. We never call IsAtWarByCallToWarAgreement directly — base handles it.
    //   2. WotR gate runs first; if it blocks, base is never reached.
    //   3. If WotR allows, base runs with the new bidirectional checks — additive safety, no conflict.
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
