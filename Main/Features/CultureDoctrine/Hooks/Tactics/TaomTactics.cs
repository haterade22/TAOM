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
    private readonly bool _defender;
    private readonly HighGroundAnchor _anchor = new HighGroundAnchor();

    public TaomTacticShieldWall(Team team, float multiplier)
        : base(team, team.Side == BattleSideEnum.Defender ? DoctrinePlans.ShieldWallDefender : DoctrinePlans.ShieldWallAttacker, multiplier)
    {
        _defender = team.Side == BattleSideEnum.Defender;
    }

    protected override float Weigh(in TeamQuerySnapshot snapshot) => DoctrineWeights.ShieldWall(in snapshot);

    protected override string StatusSuffix => _defender ? ":" + _anchor.Status : "";

    // The defender's wall stands on the navmesh high ground if the foot can get there and form
    // before the enemy's foot does, otherwise where it stands (HighGroundAnchor); the attacker's
    // plan uses BehaviorAdvance, which needs no position.
    protected override void BeforeApply(TacticPhase phase)
    {
        var infantry = MainInfantry;
        DefensePosition = _defender && infantry != null
            ? _anchor.Resolve(infantry, infantry, Archers, Team, Plan.Race, Engagement)
            : WorldPosition.Invalid;
    }

    protected override bool OnPhaseTick(TacticPhase phase)
    {
        var infantry = MainInfantry;
        return phase == TacticPhase.Defend && _defender && infantry != null && _anchor.Tick(infantry, Team, Plan.Race, Engagement);
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

    private readonly HighGroundAnchor _anchor = new HighGroundAnchor();

    public TaomTacticArcherRing(Team team, float multiplier)
        : base(team, DoctrinePlans.ArcherRing, multiplier)
    {
    }

    protected override float Weigh(in TeamQuerySnapshot snapshot) => DoctrineWeights.ArcherRing(in snapshot);

    protected override string StatusSuffix => ":" + _anchor.Status;

    // The ring stands on the high ground nearest the foreseen battleground if the infantry can
    // ring the archers there before the enemy's foot arrives, otherwise where the infantry
    // stands (HighGroundAnchor), facing the enemy. A runtime TacticalPosition is what
    // BehaviorDefensiveRing reads (position and direction only); vanilla's TacticDefensiveRing
    // constructs them the same way (`TacticDefensiveRing.cs:180`), so no scene entity is needed.
    // One allocation per apply. With no main infantry the position stays null and
    // BehaviorDefensiveRing weighs 0 (its GetAiWeight), so the row is inert, which is also what
    // the weight function reports (0 without infantry).
    protected override void BeforeApply(TacticPhase phase)
    {
        var infantry = MainInfantry;
        if (infantry == null)
        {
            RingPosition = null;
            return;
        }
        var archers = Archers;
        var position = _anchor.Resolve(infantry, archers ?? infantry, archers, Team, Plan.Race, Engagement);
        var toEnemy = Team.QuerySystem.AverageEnemyPosition - position.AsVec2;
        var direction = toEnemy.LengthSquared > 1e-4f ? toEnemy.Normalized() : infantry.Direction;
        var width = archers == null ? MinRingWidth : MathF.Max(MinRingWidth, archers.Arrangement.Width);
        RingPosition = new TacticalPosition(position, direction, width, 0f, isInsurmountable: true);
    }

    protected override bool OnPhaseTick(TacticPhase phase)
    {
        var infantry = MainInfantry;
        return phase == TacticPhase.Defend && infantry != null && _anchor.Tick(infantry, Team, Plan.Race, Engagement);
    }
}
