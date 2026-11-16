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

    /// <summary>True when a strike's instant-kill roll succeeds against this victim. False for a
    /// roll at or above the chance, a victim that is not a plain humanoid soldier (not humanoid: a
    /// horse or a creature; a hero, the player, the general, another signature bearer, an immune race,
    /// a victim shrugging off blows) or any non-finite input. The blow flag a context carries never
    /// holds a shrug-off on this path, so the shrug-off is a victim fact.</summary>
    bool DecideInstantKill(
        float chance, float roll, bool isHuman, bool isHero, bool isPlayer, bool isGeneral,
        bool isSignatureBearer, bool isKillImmuneRace, bool isShruggingOff, float health);

    /// <summary>Damage for an instant-kill blow: just over the victim's health. Zero for a
    /// non-finite or non-positive health or one the int cast cannot hold.</summary>
    int InstantKillDamage(float health);
}
