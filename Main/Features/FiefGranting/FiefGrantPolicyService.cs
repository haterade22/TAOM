using TAOM.Core.Validation;

namespace TAOM.Features.FiefGranting;

/// <summary>
/// Fief-grant scoring policy (#458, #565).
///
/// Vanilla's <c>SettlementClaimantDecision.CalculateMeritOfOutcome</c> is kept and multiplied rather
/// than replaced. Its proximity factor and settlement-value divisor are the parts worth keeping, and
/// reproducing them would mean re-deriving <c>MapDistanceModel</c> distances and
/// <c>Campaign.MapDiagonal</c> for no gain. Everything TAOM wants to change (concentration, siege
/// participation, culture fit, ruler damping) expresses cleanly as a multiplier over primitives.
///
/// Every knob is defensive against garbage: a non-finite or non-positive factor falls back to the
/// vanilla-parity value rather than inverting or erasing the ranking. `csharp-architecture.md`
/// records five shipped NaN-gate bugs, so no bare range check on a float appears here.
/// </summary>
public sealed class FiefGrantPolicyService : IFiefGrantPolicyService
{
    /// <summary>Multiplier that leaves vanilla's merit untouched.</summary>
    private const float VanillaMultiplier = 1f;

    private readonly IFiefGrantSettingsProvider _settings;

    public FiefGrantPolicyService(IFiefGrantSettingsProvider settings)
    {
        _settings = settings;
    }

    public bool IsEnabled => _settings.IsEnabled;

    public float GetMeritMultiplier(FiefGrantCandidateFacts facts)
    {
        if (!IsEnabled) return VanillaMultiplier;

        var owned = facts.OwnedFortifications > 0 ? facts.OwnedFortifications : 0;

        // The player exemption drops the terms that work AGAINST a clan for what it already holds
        // and keeps the ones that help. Siege participation is deliberately outside it (below).
        var applyPenalties = !facts.IsPlayerClan || _settings.ApplyPenaltiesToPlayerClan;

        var multiplier = VanillaMultiplier;

        // Count-based damping on top of vanilla's value-based divisor, so a clan sitting on many
        // cheap castles is damped as well as one sitting on a few rich towns.
        if (applyPenalties)
            multiplier /= 1f + owned * Penalty(_settings.ConcentrationPenalty);

        if (owned == 0)
            multiplier *= Factor(_settings.LandlessBonus);

        // Siege participation (#565). Only when the settlement has a record: with none, nobody is
        // known to have fought, and damping every clan alike would rescale the ranking for nothing.
        // A non-finite share is garbage from the boundary and falls back to vanilla parity, so the
        // positive requirement below only ever sends a real absentee to the absent factor.
        if (facts.SiegeWasRecorded && FiniteFloatValidator.IsFinite(facts.SiegeContributionShare))
        {
            if (facts.SiegeContributionShare > 0f)
            {
                // The clan that carried the assault (share 1) gets the whole bonus; the rest get it
                // in proportion to how much of the top clan's contribution they matched.
                var share = facts.SiegeContributionShare > 1f ? 1f : facts.SiegeContributionShare;
                multiplier *= 1f + (Factor(_settings.CapturerBonus) - 1f) * share;
            }
            else
            {
                // About what the clan DID, not what it holds, so the player exemption does not
                // skip it: an absent player clan is damped like any other absentee.
                multiplier *= Factor(_settings.AbsentFromSiegeFactor);
            }
        }

        // The ruling-clan factor spans 0.1 to 2.0, so it is a penalty below 1 and a BONUS above it.
        // The player exemption drops penalties and keeps bonuses, so it must look at the value
        // rather than at the term: skipping the whole term denied an exempt player ruler the very
        // bonus they had just turned the slider up to get.
        if (facts.IsRulingClan)
        {
            var rulingFactor = Factor(_settings.RulingClanFactor);
            if (applyPenalties || rulingFactor > 1f)
                multiplier *= rulingFactor;
        }

        if (facts.IsCultureMatch)
            multiplier *= Factor(_settings.CultureMatchBonus);
        else if (applyPenalties)
            multiplier *= Factor(_settings.CultureMismatchPenalty);

        return multiplier;
    }

    public bool IsKingsVoteAllowed(int rulingClanFortifications, int kingdomFortifications)
    {
        if (!IsEnabled) return true;

        // Nothing to hoard, or nothing to measure against: leave vanilla alone.
        if (kingdomFortifications <= 0 || rulingClanFortifications <= 0) return true;

        var cap = _settings.KingsVoteFiefShareCap;
        if (!FiniteFloatValidator.IsFinite(cap)) return true;

        var share = (float)rulingClanFortifications / kingdomFortifications;
        return share <= cap;
    }

    /// <summary>A multiplicative factor. Anything non-finite or non-positive reverts to vanilla parity.</summary>
    private static float Factor(float value) =>
        FiniteFloatValidator.IsFinite(value) && value > 0f ? value : VanillaMultiplier;

    /// <summary>A damping coefficient, where zero legitimately means "disabled".</summary>
    private static float Penalty(float value) =>
        FiniteFloatValidator.IsFinite(value) && value >= 0f ? value : 0f;
}
