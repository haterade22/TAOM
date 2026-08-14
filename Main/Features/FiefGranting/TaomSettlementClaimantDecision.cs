using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.FiefGranting;

/// <summary>
/// TAOM's fief-grant election (#458). Thin boundary class: it delegates conversion to
/// <see cref="FiefGrantFactsBuilder"/> and every judgement to <see cref="IFiefGrantPolicyService"/>.
///
/// Two overrides, each for a different reason.
///
/// <b><see cref="CalculateMeritOfOutcome"/></b> is where the winner is actually decided. Vanilla's
/// ballot cannot change the result: in <c>DetermineSupport</c> a clan adds
/// <c>0.2 * settlementValue * DenarsToInfluence()</c> for itself, and with <c>DenarsToInfluence()</c>
/// at 0.002 against a town worth <c>750000 + Prosperity*1000</c>, that self term dwarfs an
/// <c>InitialMerit</c> in the low tens. All three finalists reach <c>FullyPush</c>, tie at 3 points,
/// and <c>MaxBy</c> is strictly-greater so it keeps element 0 of a list already sorted by merit.
/// So merit IS the decision function, which is why this class does not touch <c>DetermineSupport</c>:
/// rescaling the self-vote would make the ballot meaningful but would not change who wins.
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
/// (written by the constructor, never read) and all three producers pass <c>null</c> anyway, so the
/// capturer signal comes from <c>Town.LastCapturedBy</c> instead. It IS pulled into the save graph
/// though, so a future engine version that starts reading it would silently see <c>null</c> here.
/// </summary>
public class TaomSettlementClaimantDecision : SettlementClaimantDecision
{
    /// <summary>
    /// Resolved lazily and at most once, never in the constructor: the save system rebuilds this
    /// object without running a constructor, so a field assigned there would be null for any
    /// decision restored from a save. <see cref="_policyResolveAttempted"/> stops a failed resolve
    /// from re-throwing on every access; the override runs 3N times per election (vanilla calls
    /// <c>NarrowDownCandidates</c> from <c>Setup</c> twice and from <c>ShouldBeCancelled</c> once),
    /// so a retry loop would be 3N exceptions per election rather than one.
    /// </summary>
    private IFiefGrantPolicyService _policy;

    private bool _policyResolveAttempted;

    public TaomSettlementClaimantDecision(
        Clan proposerClan, Settlement settlement, Hero capturerHero, Clan clanToExclude)
        : base(proposerClan, settlement, capturerHero, clanToExclude)
    {
    }

    private IFiefGrantPolicyService Policy
    {
        get
        {
            if (_policy != null || _policyResolveAttempted) return _policy;
            _policyResolveAttempted = true;

            try
            {
                _policy = IoC.Resolve<IFiefGrantPolicyService>();
            }
            catch
            {
                // Container not built yet, or the feature was not registered. Both callers below
                // fall through to vanilla when this is null.
            }

            return _policy;
        }
    }

    public override bool IsKingsVoteAllowed
    {
        get
        {
            var policy = Policy;
            if (policy == null || !policy.IsEnabled) return base.IsKingsVoteAllowed;

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

        var policy = Policy;
        if (policy == null || !policy.IsEnabled) return vanillaMerit;

        var clan = (candidateOutcome as ClanAsDecisionOutcome)?.Clan;
        if (clan == null) return vanillaMerit;

        return vanillaMerit * policy.GetMeritMultiplier(FiefGrantFactsBuilder.Build(clan, Settlement));
    }
}
