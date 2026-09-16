using TAOM.Features.SignatureStrikes.Domain;

namespace TAOM.Features.SignatureStrikes;

/// <summary>
/// The pure decisions behind a signature strike. Every input is a primitive; nothing here touches
/// an <c>Agent</c>, a <c>Blow</c> or the mission (ADR-007). The three decision methods share one
/// cooldown per strike kind: a slam is ONE package (guaranteed knockdown on the struck agent, the
/// ring, the fear burst) at most once per cooldown, and every other overhead is a vanilla hit.
/// </summary>
public interface ISignatureStrikeService
{
    bool IsEnabled { get; }

    /// <summary>The ring to apply for this collision, or null for a plain hit.</summary>
    StrikeEffect? Evaluate(in StrikeContext context);

    /// <summary>True to force the struck agent down; null to let vanilla decide.</summary>
    bool? DecideKnockdown(in StrikeContext context);

    /// <summary>True to stagger the struck agent back; null to let vanilla decide.</summary>
    bool? DecideKnockback(in StrikeContext context);

    /// <summary>Damage for one ring victim: basis x fraction x falloff, x the blocked multiplier
    /// while the victim is shield-blocking. Zero for any non-finite input or a product the int
    /// cast cannot hold.</summary>
    int ComputeRingDamage(int damageBasis, float damageFraction, float falloff, bool victimBlocking);

    /// <summary>Morale to remove from one ring victim, clamped to what the victim has. Zero for the
    /// engine's -1 "no morale" sentinel or any non-finite input.</summary>
    float ComputeFearDrain(float scaledFear, float falloff, float raceResist, float currentMorale);
}
