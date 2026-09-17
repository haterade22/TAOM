namespace TAOM.Features.CultureDoctrine.Doctrines;

/// <summary>The arrangement a braced wall stands in. Maps one to one onto the engine's
/// <c>ArrangementOrder</c> statics in the behaviour that applies it.</summary>
public enum WallStance
{
    Loose,
    Line,
    ShieldWall,
    Square,
}

/// <summary>What the wall reads each tick, all from cached engine queries.</summary>
public readonly struct WallSituation
{
    public WallSituation(bool atPosition, bool hasShield, bool cavalryChargeInbound, bool underRangedAttack, bool enemyAtArmsLength, bool withinShieldDistance)
    {
        AtPosition = atPosition;
        HasShield = hasShield;
        CavalryChargeInbound = cavalryChargeInbound;
        UnderRangedAttack = underRangedAttack;
        EnemyAtArmsLength = enemyAtArmsLength;
        WithinShieldDistance = withinShieldDistance;
    }

    /// <summary>Within <c>BehaviorDefend</c>'s 10 m of the order position.</summary>
    public bool AtPosition { get; }

    /// <summary><c>FormationQuerySystem.HasShield</c>: at least 40% of the men carry one.</summary>
    public bool HasShield { get; }

    /// <summary><c>FormationQuerySystem.IsUnderCavalryChargeFromFront</c>, 2 s cache: the closest
    /// significant enemy is cavalry, riding at us, less than 15 s away, and we face it.</summary>
    public bool CavalryChargeInbound { get; }

    public bool UnderRangedAttack { get; }

    /// <summary>The closest significant enemy is within 10 m: too late to loosen.</summary>
    public bool EnemyAtArmsLength { get; }

    /// <summary><c>BehaviorAdvance</c>'s band: the closest enemy between 10 m and 80 m while
    /// under fire, where vanilla closes the shields on the march.</summary>
    public bool WithinShieldDistance { get; }
}

/// <summary>
/// The anti-cavalry brace, and the calm stances around it, as a pure table. The engine's own
/// wall (<c>BehaviorDefend.TickOccasionally</c>, `BehaviorDefend.cs:50-85`) re-issues ShieldWall
/// or Loose every tick and never reads the cavalry query, so a wall receives a charge in the
/// same open line it stood in; this table answers Square while a charge is inbound, then holds
/// the square for <see cref="BraceHoldSeconds"/> after the signal ends (the engine's query is
/// cached 2 s and a horse that turns away 14 s out is still a horse), and only then relaxes.
/// The signal itself comes from <see cref="CavalryThreat"/> as well as the engine query, because
/// the engine's <c>IsUnderCavalryChargeFromFront</c> only sees the single closest significant
/// enemy formation and only when the wall already faces it (`FormationQuerySystem.cs:646-663`).
/// </summary>
public static class BraceDecision
{
    /// <summary>Seconds the square is kept after the charge signal last read true.</summary>
    public const float BraceHoldSeconds = 3f;

    /// <summary>A wall holding a position (the Dwarven, Dale and Uruk defence).</summary>
    public static WallStance Defend(in WallSituation s, bool bracedRecently)
    {
        if (s.CavalryChargeInbound || bracedRecently)
            return WallStance.Square;
        if (!s.AtPosition)
            return WallStance.Loose;
        if (s.HasShield)
            return WallStance.ShieldWall;
        // BehaviorDefend's rule for a wall without shields: loosen under fire while the enemy is
        // still at a distance, otherwise a line.
        return s.UnderRangedAttack && !s.EnemyAtArmsLength ? WallStance.Loose : WallStance.Line;
    }

    /// <summary>A wall on the march (the attacker's wall).</summary>
    public static WallStance Advance(in WallSituation s, bool bracedRecently)
    {
        if (s.CavalryChargeInbound || bracedRecently)
            return WallStance.Square;
        if (s.WithinShieldDistance && s.UnderRangedAttack)
            return s.HasShield ? WallStance.ShieldWall : WallStance.Loose;
        return WallStance.Line;
    }

    /// <summary>True while the square must be kept: the signal is live, or it ended less than
    /// <see cref="BraceHoldSeconds"/> ago.</summary>
    public static bool BracedRecently(float now, float lastChargeSignalTime) =>
        lastChargeSignalTime >= 0f && now - lastChargeSignalTime < BraceHoldSeconds;
}
