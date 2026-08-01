using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.Diplomacy.Models;

/// <summary>
/// #370 — the SECOND diplomacy veto path, and the one that was missed.
///
/// The engine reaches these three overrides from <c>DeclareWarDecision.IsAllowed()</c> /
/// <c>MakePeaceKingdomDecision.IsAllowed()</c>, which gate whether a kingdom decision can be
/// PROPOSED at all — a completely separate call site from the <c>ApplyInternal</c> prefixes in
/// <c>Hooks/</c>. Gating only the prefixes left this path enforcing the identical rules, including
/// <c>ShouldBlockPeace</c>, which reads <c>WarOfTheRingService.CurrentPhase</c> — TAOM
/// <c>SyncData</c> state a co-op host never replicates. A peer whose phase had drifted would
/// silently be unable to propose a peace the other peer proposes normally: no crash, no log.
/// </summary>
public class TaomKingdomDecisionPermissionModel : DefaultKingdomDecisionPermissionModel
{
    private readonly IDiplomacyService _diplomacyService;
    private readonly IWarOfTheRingService _wotrService;
    private readonly ICoopPresenceProvider _coop;

    public TaomKingdomDecisionPermissionModel(
        IDiplomacyService diplomacyService,
        IWarOfTheRingService wotrService,
        ICoopPresenceProvider coop)
    {
        _diplomacyService = diplomacyService;
        _wotrService = wotrService;
        _coop = coop;
    }

    public override bool IsStartAllianceDecisionAllowedBetweenKingdoms(
        Kingdom kingdom1, Kingdom kingdom2, out TextObject reason)
    {
        if (_coop.IsCoopActive)
            return base.IsStartAllianceDecisionAllowedBetweenKingdoms(kingdom1, kingdom2, out reason);

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
        if (!_coop.IsCoopActive
            && !_diplomacyService.IsWarAllowed(kingdom1.StringId, kingdom2.StringId))
        {
            reason = new TextObject("{=taom_war_blocked}These kingdoms are bound by an unbreakable alliance.");
            return false;
        }

        return base.IsWarDecisionAllowedBetweenKingdoms(kingdom1, kingdom2, out reason);
    }

    public override bool IsPeaceDecisionAllowedBetweenKingdoms(
        Kingdom kingdom1, Kingdom kingdom2, out TextObject reason)
    {
        if (!_coop.IsCoopActive
            && _wotrService.ShouldBlockPeace(kingdom1.StringId, kingdom2.StringId))
        {
            reason = new TextObject("{=taom_wotr_no_peace}The War of the Ring rages. There can be no peace.");
            return false;
        }

        return base.IsPeaceDecisionAllowedBetweenKingdoms(kingdom1, kingdom2, out reason);
    }
}
