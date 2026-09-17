using TAOM.Core.Validation;

namespace TAOM.Features.CultureDoctrine.Doctrines;

public enum SkirmishStage
{
    /// <summary>Closing to throwing range.</summary>
    Approaching,

    /// <summary>In range and throwing; holding the ground.</summary>
    Throwing,

    /// <summary>Enemy foot too close: fall back to range and keep throwing.</summary>
    PullingBack,

    /// <summary>Out of javelins (nobody has thrown for a while inside range): the behaviour
    /// weighs 0 from here and the plan's melee rows take the formation.</summary>
    Committed,
}

/// <summary>What the throwing infantry reads each tick, from cached engine queries.</summary>
public readonly struct SkirmishReading
{
    public SkirmishReading(bool hasEnemy, float distance, float maximumRange, float rangeAdjusted, float makingRangedAttackRatio, float throwingRatio, int unitCount, bool enemyIsInfantry, float now)
    {
        HasEnemy = hasEnemy;
        Distance = distance;
        MaximumRange = maximumRange;
        RangeAdjusted = rangeAdjusted;
        MakingRangedAttackRatio = makingRangedAttackRatio;
        ThrowingRatio = throwingRatio;
        UnitCount = unitCount;
        EnemyIsInfantry = enemyIsInfantry;
        Now = now;
    }

    public bool HasEnemy { get; }

    /// <summary>Distance to the closest significant enemy plus <c>BehaviorSkirmish</c>'s closing
    /// term (its velocity toward us, 5 to 10 s ahead by formation size).</summary>
    public float Distance { get; }

    /// <summary><c>FormationQuerySystem.MaximumMissileRange</c>: the longest throw in the formation.</summary>
    public float MaximumRange { get; }

    /// <summary><c>FormationQuerySystem.MissileRangeAdjusted</c>: the average throw with height.</summary>
    public float RangeAdjusted { get; }

    /// <summary>Share of the men who threw in the last 10 s.</summary>
    public float MakingRangedAttackRatio { get; }

    /// <summary><c>FormationQuerySystem.HasThrowingUnitRatio</c>.</summary>
    public float ThrowingRatio { get; }

    public int UnitCount { get; }
    public bool EnemyIsInfantry { get; }
    public float Now { get; }
}

/// <summary>
/// Hit-and-run for throwing infantry as a pure state machine. The engine's
/// <c>BehaviorSkirmish</c> (`BehaviorSkirmish.cs:39-194`) does exactly this dance for archers
/// (approach to range, shoot, pull back from foot that closes to 40% of range) but keys its
/// weight on the ranged class ratio, which is 0 for a javelin line, and it never runs out of
/// arrows. This copy keys on the throwing ratio and adds the fourth stage: when nobody in the
/// formation has thrown for the can't-shoot window while inside range, the javelins are gone and
/// the line commits to the melee the plan's other rows describe. Thresholds are vanilla's.
/// </summary>
public sealed class InfantrySkirmishMachine
{
    private const float PullBackSeconds = 10f;
    private const float PullBackRetrySeconds = 5f;

    /// <summary>Inside this fraction of the average throw, silence means empty hands.</summary>
    public const float SpentRangeFraction = 0.6f;

    private bool _cantShoot;
    private float _cantShootUntil;
    private float _cantShootDistance = float.MaxValue;
    private float _pullBackUntil;

    public SkirmishStage Stage { get; private set; } = SkirmishStage.Throwing;

    /// <summary>The distance the formation last found it could throw from; the approach stops at
    /// 80% of it and the pull-back at two thirds.</summary>
    public float CantShootDistance => _cantShootDistance;

    public void Reset(float now)
    {
        Stage = SkirmishStage.Throwing;
        _cantShoot = false;
        _cantShootDistance = float.MaxValue;
        _cantShootUntil = now + CantShootWindow(1);
        _pullBackUntil = 0f;
    }

    /// <summary>Vanilla's window before "we are in range but nobody throws" counts: 5 s for a
    /// handful of men, 10 s for sixty or more.</summary>
    public static float CantShootWindow(int unitCount)
    {
        var n = unitCount < 10 ? 10f : unitCount > 60 ? 60f : unitCount;
        return 5f + 5f * (n - 10f) * 0.02f;
    }

    /// <summary>Vanilla's "enough of us are throwing" bar, scaled by the throwing share.</summary>
    public static float ThrowingBar(int unitCount, float throwingRatio)
    {
        var n = unitCount < 1 ? 1f : unitCount > 50 ? 50f : unitCount;
        var t = 1f - n * 0.02f;
        return (0.1f + 0.23f * t) * throwingRatio;
    }

    /// <summary>Advance one tick; returns true when the stage changed (the order must be rebuilt).</summary>
    public bool Step(in SkirmishReading r)
    {
        if (Stage == SkirmishStage.Committed || !r.HasEnemy || !Finite(in r))
            return false;
        var bar = ThrowingBar(r.UnitCount, r.ThrowingRatio);
        var before = Stage;
        switch (Stage)
        {
            case SkirmishStage.Throwing:
                if (!(r.MakingRangedAttackRatio > bar))
                {
                    if (r.Distance > r.MaximumRange)
                    {
                        Stage = SkirmishStage.Approaching;
                        _cantShootDistance = Min(_cantShootDistance, r.MaximumRange * 0.9f);
                    }
                    else if (!_cantShoot)
                    {
                        _cantShoot = true;
                        _cantShootUntil = r.Now + CantShootWindow(r.UnitCount);
                    }
                    else if (r.Now >= _cantShootUntil)
                    {
                        // Nobody threw for the whole window. Well inside the average throw that
                        // means the javelins are spent; further out it means the longest arm
                        // set the range and the line must close (vanilla's approach branch).
                        if (r.Distance <= r.RangeAdjusted * SpentRangeFraction)
                        {
                            Stage = SkirmishStage.Committed;
                        }
                        else
                        {
                            Stage = SkirmishStage.Approaching;
                            _cantShootDistance = Min(_cantShootDistance, r.Distance);
                        }
                    }
                }
                else
                {
                    _cantShootDistance = Max(_cantShootDistance, r.Distance);
                    _cantShoot = false;
                    if (r.Now >= _pullBackUntil && r.EnemyIsInfantry && r.Distance < Min(r.RangeAdjusted * 0.4f, _cantShootDistance * 0.666f))
                    {
                        Stage = SkirmishStage.PullingBack;
                        _pullBackUntil = r.Now + PullBackSeconds;
                    }
                }
                break;
            case SkirmishStage.Approaching:
                if (r.Distance < _cantShootDistance * 0.8f || r.MakingRangedAttackRatio >= bar * 1.2f)
                {
                    Stage = SkirmishStage.Throwing;
                    _cantShoot = false;
                }
                break;
            case SkirmishStage.PullingBack:
                if (r.Distance > Min(_cantShootDistance, r.RangeAdjusted) * 0.8f)
                {
                    Stage = SkirmishStage.Throwing;
                    _cantShoot = false;
                }
                else if (r.Now >= _pullBackUntil && r.MakingRangedAttackRatio <= bar * 0.5f)
                {
                    Stage = SkirmishStage.Throwing;
                    _cantShoot = false;
                    _pullBackUntil = r.Now + PullBackRetrySeconds;
                }
                break;
        }
        return Stage != before;
    }

    private static bool Finite(in SkirmishReading r) =>
        FiniteFloatValidator.IsFinite(r.Distance) && FiniteFloatValidator.IsFinite(r.MaximumRange)
        && FiniteFloatValidator.IsFinite(r.RangeAdjusted) && FiniteFloatValidator.IsFinite(r.MakingRangedAttackRatio)
        && FiniteFloatValidator.IsFinite(r.ThrowingRatio) && FiniteFloatValidator.IsFinite(r.Now);

    private static float Min(float a, float b) => a < b ? a : b;
    private static float Max(float a, float b) => a > b ? a : b;
}
