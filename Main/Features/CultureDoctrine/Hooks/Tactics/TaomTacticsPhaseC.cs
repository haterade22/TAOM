using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Tactics;

/// <summary>
/// The seven Phase C tactics (#608). As with the Phase B four in <c>TaomTactics.cs</c>: a plan
/// from <see cref="DoctrinePlans"/>, a weight from <see cref="DoctrineWeights"/>, and where the
/// plan holds ground, positions from a <see cref="HighGroundAnchor"/> race. Type names are what
/// the sergeant popup and the status line print.
/// </summary>
public sealed class TaomTacticTwoLineWall : TaomTacticBase
{
    /// <summary>Paces between the front line's position and the second line's.</summary>
    public const float SecondLineGap = 12f;

    private readonly HighGroundAnchor _anchor = new HighGroundAnchor();

    public TaomTacticTwoLineWall(Team team, float multiplier)
        : base(team, DoctrinePlans.TwoLineWall, multiplier)
    {
    }

    protected override float Weigh(in TeamQuerySnapshot snapshot) => DoctrineWeights.TwoLineWall(in snapshot);

    protected override string StatusSuffix => ":" + _anchor.Status;

    // The front line races to the high ground as the single wall does; the second line stands a
    // gap behind it on the side away from the enemy.
    protected override void BeforeApply(TacticPhase phase)
    {
        var infantry = MainInfantry;
        if (infantry == null)
        {
            DefensePosition = WorldPosition.Invalid;
            SecondLinePosition = WorldPosition.Invalid;
            return;
        }
        var front = _anchor.Resolve(infantry, infantry, Archers, Team, Plan.Race, Engagement);
        DefensePosition = front;
        var toEnemy = Team.QuerySystem.AverageEnemyPosition - front.AsVec2;
        var facing = toEnemy.LengthSquared > 1e-4f ? toEnemy.Normalized() : infantry.Direction;
        var second = front;
        second.SetVec2(front.AsVec2 - facing * SecondLineGap);
        SecondLinePosition = second;
    }

    protected override bool OnPhaseTick(TacticPhase phase)
    {
        var infantry = MainInfantry;
        return phase == TacticPhase.Defend && infantry != null && _anchor.Tick(infantry, Team, Plan.Race, Engagement);
    }
}

public sealed class TaomTacticEnvelop : TaomTacticBase
{
    public TaomTacticEnvelop(Team team, float multiplier)
        : base(team, DoctrinePlans.Envelop, multiplier)
    {
    }

    protected override float Weigh(in TeamQuerySnapshot snapshot) => DoctrineWeights.Envelop(in snapshot);
}

public sealed class TaomTacticDisciplinedLine : TaomTacticBase
{
    private readonly bool _defender;
    private readonly HighGroundAnchor _anchor = new HighGroundAnchor();

    public TaomTacticDisciplinedLine(Team team, float multiplier)
        : base(team, team.Side == BattleSideEnum.Defender ? DoctrinePlans.DisciplinedLineDefender : DoctrinePlans.DisciplinedLineAttacker, multiplier)
    {
        _defender = team.Side == BattleSideEnum.Defender;
    }

    protected override float Weigh(in TeamQuerySnapshot snapshot) => DoctrineWeights.DisciplinedLine(in snapshot);

    protected override string StatusSuffix => _defender ? ":" + _anchor.Status : "";

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

public sealed class TaomTacticArcherAdvance : TaomTacticBase
{
    public TaomTacticArcherAdvance(Team team, float multiplier)
        : base(team, DoctrinePlans.ArcherAdvance, multiplier)
    {
    }

    protected override float Weigh(in TeamQuerySnapshot snapshot) => DoctrineWeights.ArcherAdvance(in snapshot);
}

public sealed class TaomTacticEoredScreen : TaomTacticBase
{
    private readonly HighGroundAnchor _anchor = new HighGroundAnchor();

    public TaomTacticEoredScreen(Team team, float multiplier)
        : base(team, DoctrinePlans.EoredScreen, multiplier)
    {
    }

    protected override float Weigh(in TeamQuerySnapshot snapshot) => DoctrineWeights.EoredScreen(in snapshot);

    protected override string StatusSuffix => ":" + _anchor.Status;

    protected override void BeforeApply(TacticPhase phase)
    {
        var infantry = MainInfantry;
        DefensePosition = infantry != null ? _anchor.Resolve(infantry, infantry, Archers, Team, Plan.Race, Engagement) : WorldPosition.Invalid;
    }

    protected override bool OnPhaseTick(TacticPhase phase)
    {
        var infantry = MainInfantry;
        return phase == TacticPhase.Defend && infantry != null && _anchor.Tick(infantry, Team, Plan.Race, Engagement);
    }
}

public sealed class TaomTacticHitAndRun : TaomTacticBase
{
    public TaomTacticHitAndRun(Team team, float multiplier)
        : base(team, DoctrinePlans.HitAndRun, multiplier)
    {
    }

    protected override float Weigh(in TeamQuerySnapshot snapshot) => DoctrineWeights.HitAndRun(in snapshot);
}

public sealed class TaomTacticMumakVanguard : TaomTacticBase
{
    public TaomTacticMumakVanguard(Team team, float multiplier)
        : base(team, DoctrinePlans.MumakVanguard, multiplier)
    {
    }

    protected override float Weigh(in TeamQuerySnapshot snapshot) => DoctrineWeights.MumakVanguard(in snapshot);
}
