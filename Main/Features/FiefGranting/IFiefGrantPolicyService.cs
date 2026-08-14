namespace TAOM.Features.FiefGranting;

/// <summary>
/// The fief-grant scoring policy (#458). Pure arithmetic over primitives so it is fully testable
/// without the engine; <see cref="TaomSettlementClaimantDecision"/> does the sealed-type conversion
/// at the boundary.
/// </summary>
public interface IFiefGrantPolicyService
{
    bool IsEnabled { get; }

    /// <summary>
    /// Multiplier applied on top of vanilla's <c>CalculateMeritOfOutcome</c>. Layering rather than
    /// replacing keeps vanilla's proximity and settlement-value math, which is worth keeping and
    /// expensive to reproduce. Returns 1.0 (exact vanilla) when disabled or on non-finite settings.
    /// </summary>
    float GetMeritMultiplier(FiefGrantCandidateFacts facts);

    /// <summary>
    /// Whether the ruling clan may spend influence to override the council (vanilla's King's Vote).
    /// Vanilla allows it unconditionally for fief grants, and since the king's self-support dwarfs
    /// everything else his pick is always himself, so a rich king takes fief after fief. Returns
    /// false once the ruling clan already holds more than the configured share of the kingdom.
    /// </summary>
    bool IsKingsVoteAllowed(int rulingClanFortifications, int kingdomFortifications);
}
