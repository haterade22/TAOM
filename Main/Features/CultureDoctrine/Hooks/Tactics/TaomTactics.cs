using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Tactics;

/// <summary>
/// The four Phase B tactics (#608). Each is a plan from <see cref="DoctrinePlans"/>, a weight
/// from <see cref="DoctrineWeights"/>, and where the plan needs one, a position computed just
/// before the phase table is applied. Type names are what the sergeant popup and the status
/// line print (<c>str_team_ai_tactic_text.TaomTacticShieldWall</c> and so on).
/// </summary>
public sealed class TaomTacticShieldWall : TaomTacticBase
{
    // BehaviorHoldHighGround's lock radius: it tracks the high ground while the closest enemy is
    // beyond max(0.8 * the archers' adjusted missile range, 30 m) and holds its last position
    // after that (`BehaviorHoldHighGround.cs:35-46`). The wall follows the same rule so an Engage
    // re-apply cannot walk it toward an enemy that is already on top of it.
    private const float MinLockRadius = 30f;

    private readonly bool _defender;
    private Formation? _lockedFor;
    private WorldPosition _locked = WorldPosition.Invalid;

    public TaomTacticShieldWall(Team team, float multiplier)
        : base(team, team.Side == BattleSideEnum.Defender ? DoctrinePlans.ShieldWallDefender : DoctrinePlans.ShieldWallAttacker, multiplier)
    {
        _defender = team.Side == BattleSideEnum.Defender;
    }

    protected override float Weigh(in TeamQuerySnapshot snapshot) => DoctrineWeights.ShieldWall(in snapshot);

    // The defender's wall stands on the navmesh high ground the main infantry can reach; the
    // attacker's plan uses BehaviorAdvance, which needs no position.
    protected override void BeforeApply(TacticPhase phase)
    {
        var infantry = MainInfantry;
        if (!_defender || infantry == null)
        {
            DefensePosition = WorldPosition.Invalid;
            return;
        }
        if (_lockedFor != infantry || !_locked.IsValid || EnemyBeyondLockRadius(infantry))
        {
            _locked = HighGroundOf(infantry);
            _lockedFor = infantry;
        }
        DefensePosition = _locked;
    }

    private bool EnemyBeyondLockRadius(Formation infantry)
    {
        var enemy = infantry.CachedClosestEnemyFormation;
        if (enemy == null)
            return true;
        var archers = Archers;
        var radius = MathF.Max(archers != null ? archers.QuerySystem.MissileRangeAdjusted * 0.8f : 0f, MinLockRadius);
        return infantry.CachedAveragePosition.DistanceSquared(enemy.Formation.CachedMedianPosition.AsVec2) > radius * radius;
    }
}

public sealed class TaomTacticInfantryMass : TaomTacticBase
{
    public TaomTacticInfantryMass(Team team, float multiplier)
        : base(team, DoctrinePlans.InfantryMass, multiplier)
    {
    }

    protected override float Weigh(in TeamQuerySnapshot snapshot) => DoctrineWeights.InfantryMass(in snapshot);
}

public sealed class TaomTacticCavalryDominance : TaomTacticBase
{
    public TaomTacticCavalryDominance(Team team, float multiplier)
        : base(team, DoctrinePlans.CavalryDominance, multiplier)
    {
    }

    protected override float Weigh(in TeamQuerySnapshot snapshot) => DoctrineWeights.CavalryDominance(in snapshot);
}

public sealed class TaomTacticArcherRing : TaomTacticBase
{
    // A ring narrower than this is a huddle; BehaviorDefensiveRing sizes the real circle from
    // the archers' square, the width only seeds the runtime TacticalPosition.
    private const float MinRingWidth = 10f;

    public TaomTacticArcherRing(Team team, float multiplier)
        : base(team, DoctrinePlans.ArcherRing, multiplier)
    {
    }

    protected override float Weigh(in TeamQuerySnapshot snapshot) => DoctrineWeights.ArcherRing(in snapshot);

    // The ring stands on the high ground nearest the foreseen battleground, facing the enemy.
    // A runtime TacticalPosition is what BehaviorDefensiveRing reads (position and direction
    // only); vanilla's TacticDefensiveRing constructs them the same way (`TacticDefensiveRing.cs:180`),
    // so no scene entity is needed. One allocation per apply. With no main infantry the position
    // stays null and BehaviorDefensiveRing weighs 0 (its GetAiWeight), so the row is inert, which
    // is also what the weight function reports (0 without infantry).
    protected override void BeforeApply(TacticPhase phase)
    {
        var infantry = MainInfantry;
        if (infantry == null)
        {
            RingPosition = null;
            return;
        }
        var anchor = Archers ?? infantry;
        var position = HighGroundOf(anchor);
        var toEnemy = Team.QuerySystem.AverageEnemyPosition - position.AsVec2;
        var direction = toEnemy.LengthSquared > 1e-4f ? toEnemy.Normalized() : infantry.Direction;
        var archers = Archers;
        var width = archers == null ? MinRingWidth : MathF.Max(MinRingWidth, archers.Arrangement.Width);
        RingPosition = new TacticalPosition(position, direction, width, 0f, isInsurmountable: true);
    }
}
