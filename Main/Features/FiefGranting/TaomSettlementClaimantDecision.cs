using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.FiefGranting;

/// <summary>
/// TAOM's fief-grant election (#458, #565). Thin boundary class: it delegates conversion to
/// <see cref="FiefGrantFactsBuilder"/> and every judgement to <see cref="IFiefGrantPolicyService"/>.
///
/// Two overrides, each for a different reason.
///
/// <b><see cref="CalculateMeritOfOutcome"/></b> is the lever that weights the election. Merit ranks
/// every clan, the top three go on the ballot, and every non-mercenary clan votes 1/2/3 points
/// (<c>KingdomElection.DetermineOfficialSupport</c>). A clan backs itself at roughly 40x what it
/// gives a rival in <c>DetermineSupport</c>, so the finalists usually tie and the merit order holds,
/// but not always: support costs 20/60/100 influence and <c>DetermineSupportOption</c> downgrades a
/// vote the clan cannot afford, so a top-merit finalist short on influence can lose. Merit is a
/// strong bias, not the decision function. An earlier version of this comment claimed otherwise; an
/// adversarial review pass refuted it (<c>rca-fiefgranting-2026-08-14.md</c> C2), and #460 tracks
/// whether to touch <c>DetermineSupport</c>.
///
/// <b><see cref="IsKingsVoteAllowed"/></b> closes the other door. Vanilla leaves it <c>true</c> for
/// fief grants (only <c>KingSelectionKingdomDecision</c> sets it false), and in
/// <c>KingdomElection.GetAiChoice</c> the king's preferred outcome is himself for the same self-vote
/// reason, so a ruler with influence to spare overrules the council on grant after grant. TAOM seeds
/// clans 400 to 600 influence at campaign start, well past the <c>300 + overrideCost</c> threshold.
///
/// The merit override multiplies vanilla rather than replacing it, so the proximity factor and the
/// settlement-value divisor stay exactly as TaleWorlds wrote them.
///
/// <b>Not carried across the swap:</b> vanilla's private <c>_capturerHero</c>. It is dead in v1.4.8
/// (written by the constructor, never read) and all three producers pass <c>null</c> anyway. The
/// participation signal is TAOM's own record (<see cref="IFiefSiegeParticipationService"/>), not
/// <c>Town.LastCapturedBy</c>: that stamp names one clan and is never cleared (#565). The dead field
/// IS pulled into the save graph though, so a future engine version that starts reading it would
/// silently see <c>null</c> here.
/// </summary>
public class TaomSettlementClaimantDecision : SettlementClaimantDecision
{
    /// <summary>
    /// Resolved lazily and at most once, never in the constructor: the save system rebuilds this
    /// object without running a constructor, so a field assigned there would be null for any
    /// decision restored from a save. <see cref="_resolveAttempted"/> stops a failed resolve from
    /// re-throwing on every access; the override runs 3N times per election (vanilla calls
    /// <c>NarrowDownCandidates</c> from <c>Setup</c> twice and from <c>ShouldBeCancelled</c> once),
    /// so a retry loop would be 3N exceptions per election rather than one. Both services commit
    /// together or not at all: a policy without its participation record would score every clan
    /// as if nobody had fought.
    /// </summary>
    private IFiefGrantPolicyService _policy;

    private IFiefSiegeParticipationService _participation;

    private bool _resolveAttempted;

    public TaomSettlementClaimantDecision(
        Clan proposerClan, Settlement settlement, Hero capturerHero, Clan clanToExclude)
        : base(proposerClan, settlement, capturerHero, clanToExclude)
    {
    }

    private bool TryGetServices(
        out IFiefGrantPolicyService policy, out IFiefSiegeParticipationService participation)
    {
        if (_policy == null && !_resolveAttempted)
        {
            _resolveAttempted = true;
            try
            {
                var resolvedPolicy = IoC.Resolve<IFiefGrantPolicyService>();
                var resolvedParticipation = IoC.Resolve<IFiefSiegeParticipationService>();
                _policy = resolvedPolicy;
                _participation = resolvedParticipation;
            }
            catch
            {
                // Container not built yet, or the feature was not registered. Both callers below
                // fall through to vanilla when this reports false.
            }
        }

        policy = _policy;
        participation = _participation;
        return policy != null && participation != null && policy.IsEnabled;
    }

    public override bool IsKingsVoteAllowed
    {
        get
        {
            if (!TryGetServices(out var policy, out _)) return base.IsKingsVoteAllowed;

            var kingdom = Kingdom;
            var rulingClan = kingdom?.RulingClan;
            if (kingdom == null || rulingClan == null) return base.IsKingsVoteAllowed;

            return policy.IsKingsVoteAllowed(
                FiefGrantFactsBuilder.CountClanFortifications(rulingClan, Settlement),
                FiefGrantFactsBuilder.CountKingdomFortifications(kingdom, Settlement));
        }
    }

    public override float CalculateMeritOfOutcome(DecisionOutcome candidateOutcome)
    {
        var vanillaMerit = base.CalculateMeritOfOutcome(candidateOutcome);

        if (!TryGetServices(out var policy, out var participation)) return vanillaMerit;

        var clan = (candidateOutcome as ClanAsDecisionOutcome)?.Clan;
        if (clan == null) return vanillaMerit;

        return vanillaMerit * policy.GetMeritMultiplier(
            FiefGrantFactsBuilder.Build(clan, Settlement, participation));
    }
}
