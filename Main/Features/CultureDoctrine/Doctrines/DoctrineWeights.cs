using System;
using TAOM.Core.Validation;

namespace TAOM.Features.CultureDoctrine.Doctrines;

/// <summary>
/// One team's <c>TeamQuerySystem</c> reduced to the numbers the weight functions read, taken
/// once per <c>GetTacticWeight</c> call on the AI thread. Every engine ratio is already cached
/// (2.5 s to 15 s), so the snapshot costs a dozen property reads.
/// </summary>
public readonly struct TeamQuerySnapshot
{
    public TeamQuerySnapshot(
        int memberCount, int enemyUnitCount, float infantryRatio, float rangedRatio, float cavalryRatio,
        float rangedCavalryRatio, float remainingPowerRatio, float notEngagingAdvantage,
        bool hasInfantry, bool hasArchers, bool hasCavalry, bool isDefenseApplicable, bool ringFits, bool isDefender)
        : this(memberCount, enemyUnitCount, infantryRatio, rangedRatio, cavalryRatio, rangedCavalryRatio, remainingPowerRatio,
            notEngagingAdvantage, hasInfantry, hasArchers, hasCavalry, isDefenseApplicable, ringFits, isDefender,
            infantryCount: (int)(memberCount * infantryRatio), throwingRatio: 0f, hasVanguard: false,
            infantryTotal: (int)(memberCount * infantryRatio))
    {
    }

    public TeamQuerySnapshot(
        int memberCount, int enemyUnitCount, float infantryRatio, float rangedRatio, float cavalryRatio,
        float rangedCavalryRatio, float remainingPowerRatio, float notEngagingAdvantage,
        bool hasInfantry, bool hasArchers, bool hasCavalry, bool isDefenseApplicable, bool ringFits, bool isDefender,
        int infantryCount, float throwingRatio, bool hasVanguard)
        : this(memberCount, enemyUnitCount, infantryRatio, rangedRatio, cavalryRatio, rangedCavalryRatio, remainingPowerRatio,
            notEngagingAdvantage, hasInfantry, hasArchers, hasCavalry, isDefenseApplicable, ringFits, isDefender,
            infantryCount, throwingRatio, hasVanguard, infantryTotal: infantryCount)
    {
    }

    public TeamQuerySnapshot(
        int memberCount, int enemyUnitCount, float infantryRatio, float rangedRatio, float cavalryRatio,
        float rangedCavalryRatio, float remainingPowerRatio, float notEngagingAdvantage,
        bool hasInfantry, bool hasArchers, bool hasCavalry, bool isDefenseApplicable, bool ringFits, bool isDefender,
        int infantryCount, float throwingRatio, bool hasVanguard, int infantryTotal)
    {
        MemberCount = memberCount;
        EnemyUnitCount = enemyUnitCount;
        InfantryRatio = infantryRatio;
        RangedRatio = rangedRatio;
        CavalryRatio = cavalryRatio;
        RangedCavalryRatio = rangedCavalryRatio;
        RemainingPowerRatio = remainingPowerRatio;
        NotEngagingAdvantage = notEngagingAdvantage;
        HasInfantry = hasInfantry;
        HasArchers = hasArchers;
        HasCavalry = hasCavalry;
        IsDefenseApplicable = isDefenseApplicable;
        RingFits = ringFits;
        IsDefender = isDefender;
        InfantryCount = infantryCount;
        ThrowingRatio = throwingRatio;
        HasVanguard = hasVanguard;
        InfantryTotal = infantryTotal;
    }

    public int MemberCount { get; }
    public int EnemyUnitCount { get; }
    public float InfantryRatio { get; }
    public float RangedRatio { get; }
    public float CavalryRatio { get; }
    public float RangedCavalryRatio { get; }
    public float RemainingPowerRatio { get; }

    /// <summary><c>TacticComponent.CalculateNotEngagingTacticalAdvantage</c>: 1 to 1.5^1.5 from
    /// the cavalry balance, vanilla's "we can afford to wait" factor.</summary>
    public float NotEngagingAdvantage { get; }

    public bool HasInfantry { get; }
    public bool HasArchers { get; }
    public bool HasCavalry { get; }

    /// <summary><c>TeamAIComponent.IsDefenseApplicable</c>: false for every attacker, and for a
    /// defender being out-shot. The ring honours it; the wall does not.</summary>
    public bool IsDefenseApplicable { get; }

    /// <summary><see cref="RingGeometry.Fits"/> for the main infantry and archer formations.</summary>
    public bool RingFits { get; }
    public bool IsDefender { get; }

    /// <summary>Men in the largest infantry formation.</summary>
    public int InfantryCount { get; }

    /// <summary>Men in every infantry formation together. The gates that ask "are there enough
    /// foot for two lines or three blocks" read this, not <see cref="InfantryCount"/>: a tactic
    /// that splits the infantry halves or thirds the largest formation on its own apply, and a
    /// gate on the largest would cancel the tactic at the next decision (found 2026-09-17).</summary>
    public int InfantryTotal { get; }

    /// <summary><c>FormationQuerySystem.HasThrowingUnitRatio</c> of the largest infantry formation.</summary>
    public float ThrowingRatio { get; }

    /// <summary>The team's HeavyCavalry formation holds troops (a routed vanguard).</summary>
    public bool HasVanguard { get; }
}

/// <summary>
/// The weight each TAOM tactic reports, scaled like vanilla's so the wrapped vanilla tactics
/// beside it compete on the same axis: vanilla's defensive tactics are
/// <c>(Inf + Rng) * 1.1..1.2 * advantage / sqrt(RemainingPowerRatio)</c>, its offensive ones a
/// class ratio times <c>sqrt(RemainingPowerRatio)</c>. Every function is a positive requirement
/// (a NaN input fails every comparison and returns 0) and clamps the power ratio away from 0.
/// </summary>
public static class DoctrineWeights
{
    private const float MinPowerRatio = 0.01f;

    /// <summary>Dwarves: infantry and ranged share, holding whether attacking or defending.</summary>
    public static float ShieldWall(in TeamQuerySnapshot s)
    {
        if (!s.HasInfantry || !Finite(s))
            return 0f;
        return (s.InfantryRatio + s.RangedRatio) * 1.2f * s.NotEngagingAdvantage / Root(s.RemainingPowerRatio);
    }

    /// <summary>Mordor and Gundabad: infantry share scaled by numbers, clamped to [0.5, 2] so a
    /// horde presses harder and a remnant turns cautious.</summary>
    public static float InfantryMass(in TeamQuerySnapshot s)
    {
        if (!s.HasInfantry || !Finite(s))
            return 0f;
        var numbers = Clamp(s.MemberCount / (float)Math.Max(1, s.EnemyUnitCount), 0.5f, 2f);
        return 1.5f * s.InfantryRatio * numbers * (float)Math.Sqrt(Math.Max(MinPowerRatio, s.RemainingPowerRatio));
    }

    /// <summary>Rohan: vanilla's <c>TacticFrontalCavalryCharge</c> formula times 1.3.</summary>
    public static float CavalryDominance(in TeamQuerySnapshot s)
    {
        if (!s.HasCavalry || !Finite(s))
            return 0f;
        var nonHorseArchers = Math.Max(1f, s.MemberCount - s.RangedCavalryRatio * s.MemberCount);
        return 1.3f * s.CavalryRatio * s.MemberCount / nonHorseArchers * (float)Math.Sqrt(Math.Max(MinPowerRatio, s.RemainingPowerRatio));
    }

    /// <summary>Elves defending: vanilla's <c>TacticDefensiveRing</c> formula
    /// (<c>min(Inf, Rng) * 2 * 1.5</c>, the 1.5 being its DefendersAdvantage) at a position score
    /// of 1, gated the way vanilla gates it. The navmesh ring has no scene score to apply.</summary>
    public static float ArcherRing(in TeamQuerySnapshot s)
    {
        if (!s.IsDefender || !s.IsDefenseApplicable || !s.HasInfantry || !s.HasArchers || !s.RingFits || !Finite(s))
            return 0f;
        return Math.Min(s.InfantryRatio, s.RangedRatio) * 3f * s.NotEngagingAdvantage / Root(s.RemainingPowerRatio);
    }

    /// <summary>The edge a gated variant holds over the tactic it refines while its gate passes.
    /// <c>TeamAIComponent.MakeDecision</c> keeps the current tactic unless a challenger beats it
    /// by 1.5x (`TeamAIComponent.cs:301`), so an edge under 1.5 could never take the team back
    /// once the plain tactic held it; above 1.5 the variant wins whenever its gate passes and
    /// yields the moment it fails, one switch per crossing.</summary>
    public const float GatedEdge = 1.6f;

    /// <summary>Two-line wall: the wall's weight with the gated edge, only for an army with the
    /// men to form two lines (counted across every infantry formation, because the tactic's own
    /// split halves the largest one); a smaller army keeps the single wall.</summary>
    public const int TwoLineMinInfantry = 80;

    public static float TwoLineWall(in TeamQuerySnapshot s) =>
        s.InfantryTotal < TwoLineMinInfantry ? 0f : ShieldWall(in s) * GatedEdge;

    /// <summary>Envelopment: the mass's weight with the gated edge, only when the horde outnumbers
    /// the enemy by <see cref="EnvelopMinNumbers"/> and has <see cref="EnvelopMinInfantry"/> men
    /// across its infantry to split three ways; otherwise the plain mass.</summary>
    public const float EnvelopMinNumbers = 1.2f;
    public const int EnvelopMinInfantry = 60;

    public static float Envelop(in TeamQuerySnapshot s)
    {
        if (s.InfantryTotal < EnvelopMinInfantry || !Finite(s))
            return 0f;
        var numbers = s.MemberCount / (float)Math.Max(1, s.EnemyUnitCount);
        return numbers < EnvelopMinNumbers ? 0f : InfantryMass(in s) * GatedEdge;
    }

    /// <summary>Gondor, Rhun, Dale: a disciplined combined-arms line; the wall's shape of
    /// weight with a higher constant, both sides.</summary>
    public static float DisciplinedLine(in TeamQuerySnapshot s)
    {
        if (!s.HasInfantry || !Finite(s))
            return 0f;
        return (s.InfantryRatio + s.RangedRatio) * 1.5f * s.NotEngagingAdvantage / Root(s.RemainingPowerRatio);
    }

    /// <summary>Elves attacking: the ring's balance term as an offensive weight; attacker only,
    /// the ring is the defender's doctrine.</summary>
    public static float ArcherAdvance(in TeamQuerySnapshot s)
    {
        if (s.IsDefender || !s.HasInfantry || !s.HasArchers || !Finite(s))
            return 0f;
        return Math.Min(s.InfantryRatio, s.RangedRatio) * 2f * (float)Math.Sqrt(Math.Max(MinPowerRatio, s.RemainingPowerRatio));
    }

    /// <summary>Rohan defending: the cavalry share, defender only, above the dominance charge so
    /// a defending eored screens first and cycle-charges once joined.</summary>
    public static float EoredScreen(in TeamQuerySnapshot s)
    {
        if (!s.IsDefender || !s.HasCavalry || !Finite(s))
            return 0f;
        return s.CavalryRatio * 1.6f * s.NotEngagingAdvantage / Root(s.RemainingPowerRatio);
    }

    /// <summary>Dunland attacking: the infantry share scaled by how much of the line throws;
    /// a line without javelins has nothing to hit and run with.</summary>
    public static float HitAndRun(in TeamQuerySnapshot s)
    {
        if (!s.HasInfantry || !Finite(s) || !FiniteFloatValidator.IsFinite(s.ThrowingRatio))
            return 0f;
        var throwing = Clamp(s.ThrowingRatio / 0.5f, 0f, 1f);
        return s.InfantryRatio * 1.4f * throwing * (float)Math.Sqrt(Math.Max(MinPowerRatio, s.RemainingPowerRatio));
    }

    /// <summary>Harad attacking with mumakil on the field: infantry and cavalry share behind the
    /// beasts; 0 without a routed vanguard formation.</summary>
    public static float MumakVanguard(in TeamQuerySnapshot s)
    {
        if (!s.HasVanguard || !s.HasInfantry || !Finite(s))
            return 0f;
        return (s.InfantryRatio + s.CavalryRatio) * 1.4f * (float)Math.Sqrt(Math.Max(MinPowerRatio, s.RemainingPowerRatio));
    }

    private static float Root(float remainingPowerRatio) =>
        (float)Math.Sqrt(Math.Max(MinPowerRatio, remainingPowerRatio));

    private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

    private static bool Finite(in TeamQuerySnapshot s) =>
        FiniteFloatValidator.IsFinite(s.InfantryRatio) && FiniteFloatValidator.IsFinite(s.RangedRatio)
        && FiniteFloatValidator.IsFinite(s.CavalryRatio) && FiniteFloatValidator.IsFinite(s.RangedCavalryRatio)
        && FiniteFloatValidator.IsFinite(s.RemainingPowerRatio) && FiniteFloatValidator.IsFinite(s.NotEngagingAdvantage);
}

/// <summary>Vanilla's ring feasibility (`TacticDefensiveRing.cs:120-127`): the radius the
/// infantry can ring at maximum spacing against the side of the archers' square.</summary>
public static class RingGeometry
{
    public static bool Fits(int infantryCount, float infantryMaxInterval, float infantryUnitDiameter, int archerCount, float archerUnitDiameter, float archerInterval)
    {
        if (infantryCount <= 0 || archerCount <= 0)
            return false;
        var radius = infantryCount * (infantryMaxInterval + infantryUnitDiameter) / (2f * (float)Math.PI);
        var side = (float)Math.Sqrt(archerCount);
        var squareSide = archerUnitDiameter * side + archerInterval * (side - 1f);
        return FiniteFloatValidator.IsFinite(radius) && FiniteFloatValidator.IsFinite(squareSide) && radius >= squareSide;
    }
}
