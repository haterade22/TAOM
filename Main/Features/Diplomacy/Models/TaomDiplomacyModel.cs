using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Features.CoopInterop;
using TAOM.Features.CulturalFeats;

namespace TAOM.Features.Diplomacy.Models;

public class TaomDiplomacyModel : DefaultDiplomacyModel
{
    private readonly IWarOfTheRingService _wotrService;
    private readonly ICoopSessionProvider _coop;

    public TaomDiplomacyModel(IWarOfTheRingService wotrService, ICoopSessionProvider coop)
    {
        _wotrService = wotrService;
        _coop = coop;
    }

    /// <summary>
    /// #370 — the THIRD site reading <c>ShouldBlockPeace</c>, and the one the review agent itself
    /// missed while reporting the other two. "Constantly at war" makes peace permanently
    /// unreachable between two factions, so a peer whose WotR phase had drifted would hold a
    /// different view of which wars can ever end. Same gate as the prefixes and the permission
    /// model: under co-op the host's diplomacy governs.
    /// </summary>
    public override bool IsAtConstantWar(IFaction faction1, IFaction faction2)
    {
        if (!_coop.ShouldDeferToHost
            && _wotrService.IsWarOfTheRingActive
            && _wotrService.ShouldBlockPeace(faction1.StringId, faction2.StringId))
        {
            return true;
        }

        return base.IsAtConstantWar(faction1, faction2);
    }

    public override int GetRelationChangeAfterVotingInSettlementOwnerPreliminaryDecision(
        Hero supporter, bool hasHeroVotedAgainstOwner)
    {
        int result = base.GetRelationChangeAfterVotingInSettlementOwnerPreliminaryDecision(
            supporter, hasHeroVotedAgainstOwner);

        if (hasHeroVotedAgainstOwner
            && supporter.Culture?.HasFeat(TaomCulturalFeats.IsengardDecisionPenaltyFeat) == true)
        {
            result += (int)(result * TaomCulturalFeats.IsengardDecisionPenaltyFeat.EffectBonus);
        }

        return result;
    }
}
