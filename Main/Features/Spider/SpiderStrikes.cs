using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;

namespace TAOM.Features.Spider;

/// <summary>
/// One spider attack's rules as data (#692): how many enemies in its arc it may strike, a multiplier on its damage, and
/// whether only a crit knocks the victim down. <see cref="Ridden"/> is today's ridden spider exactly: every enemy in the
/// arc, full damage, a knockdown at <see cref="SpiderConfig.DamageToFall"/>. Creature bandits tune their own
/// (CreatureBanditTuning). Immutable; inputs are clamped, so a bad setting cannot break a strike.
/// </summary>
public sealed class SpiderStrikeProfile
{
    public static readonly SpiderStrikeProfile Ridden = new(int.MaxValue, 1f, knockdownOnCritOnly: false);

    public SpiderStrikeProfile(int maxTargets, float damageMultiplier, bool knockdownOnCritOnly)
    {
        MaxTargets = maxTargets < 1 ? 1 : maxTargets;
        DamageMultiplier = float.IsNaN(damageMultiplier) || float.IsInfinity(damageMultiplier) ? 1f
            : damageMultiplier < 0f ? 0f : damageMultiplier;
        KnockdownOnCritOnly = knockdownOnCritOnly;
    }

    public int MaxTargets { get; }
    public float DamageMultiplier { get; }
    public bool KnockdownOnCritOnly { get; }
    public bool IsCapped => MaxTargets != int.MaxValue;
}

/// <summary>
/// The strike for each clip the service chooses: the standing front strike is the bite, the running lunge the pounce,
/// the left and right swings the swipe. An unknown clip takes the bite, the narrowest.
/// </summary>
public sealed class SpiderStrikeSet
{
    public static readonly SpiderStrikeSet Ridden = new(SpiderStrikeProfile.Ridden, SpiderStrikeProfile.Ridden, SpiderStrikeProfile.Ridden);

    public SpiderStrikeSet(SpiderStrikeProfile bite, SpiderStrikeProfile pounce, SpiderStrikeProfile swipe)
    {
        Bite = bite;
        Pounce = pounce;
        Swipe = swipe;
    }

    public SpiderStrikeProfile Bite { get; }
    public SpiderStrikeProfile Pounce { get; }
    public SpiderStrikeProfile Swipe { get; }

    public SpiderStrikeProfile For(string clipName) => clipName switch
    {
        SpiderConfig.PounceChargeActionName => Pounce,
        SpiderConfig.SwingLeftActionName or SpiderConfig.SwingRightActionName => Swipe,
        _ => Bite,
    };
}

/// <summary>The pure damage and reaction rules of a strike.</summary>
public static class SpiderStrikes
{
    public static int Scale(int damage, SpiderStrikeProfile strike) => (int)(damage * strike.DamageMultiplier);

    /// <summary>
    /// The victim's reaction: nothing below the flinch threshold; then, for a crit-only strike, a knockdown on a crit and a
    /// stagger otherwise; for any other strike, today's thresholds (flinch, then a knockdown at DamageToFall).
    /// </summary>
    public static DamageAnimation Reaction(int damage, bool isCrit, SpiderStrikeProfile strike)
    {
        if (damage < SpiderConfig.DamageToFlinch) return DamageAnimation.Nothing;
        if (strike.KnockdownOnCritOnly) return isCrit ? DamageAnimation.Fall : DamageAnimation.Flinch;
        return damage < SpiderConfig.DamageToFall ? DamageAnimation.Flinch : DamageAnimation.Fall;
    }
}

/// <summary>
/// What one attack did: the clip it played, the agents its arc reported, the allies among them (counted for a capped
/// strike only), the enemies it struck and its cap. The creature bandit's attack task logs it (#692).
/// </summary>
public readonly struct SpiderStrikeOutcome
{
    public SpiderStrikeOutcome(string clip, int inArc, int allies, int struck, int maxTargets)
    {
        Clip = clip;
        InArc = inArc;
        Allies = allies;
        Struck = struck;
        MaxTargets = maxTargets;
    }

    public string Clip { get; }
    public int InArc { get; }
    public int Allies { get; }
    public int Struck { get; }
    public int MaxTargets { get; }
}

/// <summary>
/// An enemy in a capped strike's arc: its index in the arc, squared distance, and the index of its rider in the same
/// arc, or -1 when it has none there.
/// </summary>
public readonly struct SpiderStrikeCandidate
{
    public SpiderStrikeCandidate(int id, float distanceSquared, int riderId)
    {
        Id = id;
        DistanceSquared = distanceSquared;
        RiderId = riderId;
    }

    public int Id { get; }
    public float DistanceSquared { get; }
    public int RiderId { get; }
}

/// <summary>Which enemies a capped strike hits.</summary>
public static class SpiderStrikeTargets
{
    /// <summary>
    /// The nearest <paramref name="maxTargets"/> candidates, stable on ties. A horse whose rider is also in the arc is
    /// dropped, so a cavalryman and his mount use one slot (the rider takes the strike).
    /// </summary>
    public static IReadOnlyList<int> Pick(IReadOnlyList<SpiderStrikeCandidate> candidates, int maxTargets)
    {
        return candidates
            .Where(c => c.RiderId < 0)
            .OrderBy(c => c.DistanceSquared)
            .Take(maxTargets)
            .Select(c => c.Id)
            .ToList();
    }
}
