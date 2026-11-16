using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.HostilePeace.Models;

/// <summary>
/// Block B: no trade agreement between two Hostile realms. Every vanilla path asks <c>CanMakeTradeAgreement</c> (v1.5.7, read on
/// the installed DLL 2026-11-15): the AI's proposal (<c>KingdomDecisionProposalBehavior.ConsiderTradeAgreement</c>), the decision's
/// <c>TradeAgreementDecision.CanMakeDecision</c> and its cancel check (<c>ShouldBeCancelledInternal</c>), which drops one already
/// proposed.
/// An agreement signed before this rule (an old save) runs to its end date. Off under co-op on a peer that defers to the host,
/// like <c>TaomDiplomacyModel</c>: the host's diplomacy governs.
/// </summary>
public sealed class TaomTradeAgreementModel : DefaultTradeAgreementModel
{
    private readonly HostilePeaceService _service;
    private readonly ICoopSessionProvider _coop;

    public TaomTradeAgreementModel(HostilePeaceService service, ICoopSessionProvider coop)
    {
        _service = service;
        _coop = coop;
    }

    public override bool CanMakeTradeAgreement(Kingdom querierKingdom, Kingdom queriedKingdom, bool checkOtherSideSupport,
        out TextObject reason, bool includeReason = false)
    {
        if (!_coop.ShouldDeferToHost && _service.BlocksTradeAgreement(querierKingdom?.StringId, queriedKingdom?.StringId))
        {
            reason = HostilePeaceTexts.NoTradeAgreement();
            return false;
        }

        return base.CanMakeTradeAgreement(querierKingdom, queriedKingdom, checkOtherSideSupport, out reason, includeReason);
    }
}
