using TAOM.Core.Validation;

namespace TAOM.Features.CultureDoctrine.Doctrines;

public enum ChargeStage
{
    /// <summary>Riding at the target and fighting through it.</summary>
    Charging,

    /// <summary>Past the target: ride clear to the reform point instead of milling in the melee.</summary>
    RidingThrough,

    /// <summary>At the reform point: gather, turn to face the enemy, then go again.</summary>
    Reforming,
}

/// <summary>What the cavalry reads each tick, from cached engine data.</summary>
public readonly struct ChargeReading
{
    public ChargeReading(bool hasTarget, float distanceToTarget, float stopDistance, bool passedTarget, bool gathered, float secondsInStage)
    {
        HasTarget = hasTarget;
        DistanceToTarget = distanceToTarget;
        StopDistance = stopDistance;
        PassedTarget = passedTarget;
        Gathered = gathered;
        SecondsInStage = secondsInStage;
    }

    public bool HasTarget { get; }
    public float DistanceToTarget { get; }

    /// <summary>How far past the target this charge pulls up (<see cref="CycleTunables.StopDistanceFor"/>,
    /// fixed when the charge began).</summary>
    public float StopDistance { get; }

    /// <summary>The direction the charge began in now points away from the target: the
    /// formation's average position has gone through it.</summary>
    public bool PassedTarget { get; }

    /// <summary>The riders are back in their slots (position deviation under half the average
    /// top speed, <c>BehaviorAdvance</c>'s own spread test).</summary>
    public bool Gathered { get; }

    public float SecondsInStage { get; }
}

public readonly struct CycleTunables
{
    public CycleTunables(float maxMeleeSeconds, float rideOutSeconds, float minReformSeconds, float maxReformSeconds, float contactDistance, float minStopDistance, float maxStopDistance)
    {
        MaxMeleeSeconds = maxMeleeSeconds;
        RideOutSeconds = rideOutSeconds;
        MinReformSeconds = minReformSeconds;
        MaxReformSeconds = maxReformSeconds;
        ContactDistance = contactDistance;
        MinStopDistance = minStopDistance;
        MaxStopDistance = maxStopDistance;
    }

    /// <summary>Vanilla's disabled cavalry cycle (<c>BehaviorTacticalCharge</c>: 5 s ride-out,
    /// 2 s reform, 30 m contact) with a 10 s cap on the melee and a 35 to 60 m stop distance:
    /// vanilla's 20 to 50 m was written for infantry, and a reform point inside the contact
    /// distance would end every reform on its first tick (<see cref="CycleChargeMachine.Next"/>
    /// also halves the contact distance against the stop distance for that reason).</summary>
    public static readonly CycleTunables Default = new CycleTunables(
        maxMeleeSeconds: 10f, rideOutSeconds: 5f, minReformSeconds: 2f, maxReformSeconds: 8f,
        contactDistance: 30f, minStopDistance: 35f, maxStopDistance: 60f);

    /// <summary>Longest the riders stay in the melee before they are pulled out whether or not
    /// they got through; a charge that bogs down is the one that loses horses.</summary>
    public float MaxMeleeSeconds { get; }
    public float RideOutSeconds { get; }
    public float MinReformSeconds { get; }
    public float MaxReformSeconds { get; }

    /// <summary>An enemy this close to a reforming formation ends the reform: charge it. The
    /// reform point stands at the stop distance from the enemy, so the working threshold is the
    /// smaller of this and half the stop distance (<see cref="ReformContactDistance"/>).</summary>
    public float ContactDistance { get; }

    /// <summary>The distance at which an enemy interrupts a reform whose point stands
    /// <paramref name="stopDistance"/> from it: never more than half way.</summary>
    public float ReformContactDistance(float stopDistance)
    {
        if (!FiniteFloatValidator.IsFinite(stopDistance))
            return ContactDistance;
        var half = stopDistance * 0.5f;
        return half < ContactDistance ? half : ContactDistance;
    }
    public float MinStopDistance { get; }
    public float MaxStopDistance { get; }

    /// <summary>How far past the target the riders pull up: the charge distance, clamped.</summary>
    public float StopDistanceFor(float initialDistance)
    {
        if (!FiniteFloatValidator.IsFinite(initialDistance))
            return MinStopDistance;
        return initialDistance < MinStopDistance ? MinStopDistance : initialDistance > MaxStopDistance ? MaxStopDistance : initialDistance;
    }
}

/// <summary>
/// The cycle charge as a pure state machine: charge through, ride clear, reform, repeat. It is
/// the machine <c>BehaviorTacticalCharge</c> carries (`BehaviorTacticalCharge.cs:59-139`) and
/// short-circuits for cavalry (`:149-153`, a bare <c>ChargeToTarget</c>), lifted out so it can
/// be tested and driven from a TAOM behaviour. Every comparison is a positive requirement, so a
/// NaN distance or time never advances a stage.
/// </summary>
public sealed class CycleChargeMachine
{
    public ChargeStage Stage { get; private set; }

    public void Reset() => Stage = ChargeStage.Charging;

    /// <summary>Advance the machine one tick; returns true when the stage changed.</summary>
    public bool Step(in ChargeReading r, in CycleTunables t)
    {
        var next = Next(Stage, in r, in t);
        if (next == Stage)
            return false;
        Stage = next;
        return true;
    }

    public static ChargeStage Next(ChargeStage stage, in ChargeReading r, in CycleTunables t)
    {
        if (!r.HasTarget)
            return ChargeStage.Charging;
        switch (stage)
        {
            case ChargeStage.Charging:
                return r.PassedTarget || r.SecondsInStage > t.MaxMeleeSeconds ? ChargeStage.RidingThrough : ChargeStage.Charging;
            case ChargeStage.RidingThrough:
                // The ride ends when the riders are clear of the target or the timer runs out.
                return r.DistanceToTarget >= r.StopDistance || r.SecondsInStage > t.RideOutSeconds ? ChargeStage.Reforming : ChargeStage.RidingThrough;
            case ChargeStage.Reforming:
                if (r.DistanceToTarget <= t.ReformContactDistance(r.StopDistance))
                    return ChargeStage.Charging;
                if (r.SecondsInStage >= t.MinReformSeconds && (r.Gathered || r.SecondsInStage >= t.MaxReformSeconds))
                    return ChargeStage.Charging;
                return ChargeStage.Reforming;
            default:
                return ChargeStage.Charging;
        }
    }
}
