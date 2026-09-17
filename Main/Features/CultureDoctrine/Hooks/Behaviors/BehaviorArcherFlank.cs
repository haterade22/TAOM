using System.Collections.Generic;
using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Behaviors;

/// <summary>
/// Archers on the flank of a wall (<see cref="ArcherFlankGeometry"/>): beside the main infantry's
/// order position, a gap out and a little back, facing the enemy, Loose, fire at will, the
/// side chosen once at activation where fewer enemy formations stand. While an enemy formation
/// is closing on the archers rather than on the wall they stand straight behind the wall, and
/// return once it is well away. The wall is <see cref="Wall"/>, set by the tactic before the
/// plan is applied (as <c>BehaviorHoldHighGround.RangedAllyFormation</c> is); without one, or
/// once it is empty, the behaviour weighs 0 and the plan's <c>SkirmishLine</c> row takes the
/// archers. The enemy walk runs on the active tick only; the plan reads what it left.
/// </summary>
public sealed class BehaviorArcherFlank : TaomBehaviorBase
{
    private readonly List<Vec2> _enemyPositions = new List<Vec2>(8);
    private ArcherFlankGeometry.FlankSide _side = ArcherFlankGeometry.FlankSide.Right;
    private bool _fallingBack;
    private bool _sideChosen;

    /// <summary>Set by the tactic before the plan is applied; null means "no wall to flank".</summary>
    public Formation? Wall;

    public BehaviorArcherFlank(Formation formation)
        : base(formation)
    {
        BehaviorCoherence = 0.6f;
        CalculateCurrentOrder();
    }

    protected override float Weigh() => Wall != null && Wall.CountOfUnits > 0 && Wall != Formation ? 1f : 0f;

    protected override void Plan()
    {
        var formation = Formation;
        var wall = Wall;
        if (wall == null || wall.CountOfUnits <= 0)
        {
            CurrentOrder = MovementOrder.MovementOrderMove(WallStances.Here(formation));
            CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;
            return;
        }
        var wallCentre = wall.OrderPosition;
        var toEnemy = formation.QuerySystem.Team.AverageEnemyPosition - wallCentre;
        var facing = toEnemy.LengthSquared > 1e-4f ? toEnemy.Normalized() : wall.Direction;
        var point = _fallingBack
            ? ArcherFlankGeometry.BehindPoint(wallCentre, facing, ArcherFlankGeometry.BehindDistance)
            : ArcherFlankGeometry.FlankPoint(wallCentre, facing, wall.Width, formation.Width, _side, ArcherFlankGeometry.Gap, ArcherFlankGeometry.SetBack);
        var position = wall.CachedMedianPosition;
        position.SetVec2(point);
        CurrentOrder = MovementOrder.MovementOrderMove(position);
        CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;
    }

    protected override void Activate()
    {
        _fallingBack = false;
        _sideChosen = false;
        Formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderLoose);
        Formation.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
        Formation.SetFormOrder(FormOrder.FormOrderWide);
        SetStatus("Flank");
    }

    protected override void OnActiveTick()
    {
        var formation = Formation;
        var wall = Wall;
        if (wall == null || wall.CountOfUnits <= 0)
            return;
        // One walk over the enemy: positions for the side choice, the nearest to us and how far
        // that one is from the wall for the fall-back rule.
        _enemyPositions.Clear();
        var ours = formation.CachedAveragePosition;
        var wallCentre = wall.CachedAveragePosition;
        var nearest = float.PositiveInfinity;
        var nearestToWall = float.PositiveInfinity;
        var teams = formation.Team.Mission.Teams;
        for (var t = 0; t < teams.Count; t++)
        {
            var other = teams[t];
            if (other == formation.Team || !other.IsEnemyOf(formation.Team))
                continue;
            var formations = other.FormationsIncludingSpecialAndEmpty;
            for (var i = 0; i < formations.Count; i++)
            {
                var f = formations[i];
                if (f.CountOfUnits <= 0)
                    continue;
                var p = f.CachedMedianPosition.AsVec2;
                _enemyPositions.Add(p);
                var d = ours.Distance(p);
                if (d < nearest)
                {
                    nearest = d;
                    nearestToWall = wallCentre.Distance(p);
                }
            }
        }
        if (!_sideChosen)
        {
            var toEnemy = formation.QuerySystem.Team.AverageEnemyPosition - wallCentre;
            _side = ArcherFlankGeometry.ChooseSide(wallCentre, toEnemy.LengthSquared > 1e-4f ? toEnemy.Normalized() : wall.Direction, _enemyPositions);
            _sideChosen = true;
        }
        var fallingBack = ArcherFlankGeometry.ShouldFallBack(nearest, nearestToWall, ArcherFlankGeometry.FallBackTrigger, _fallingBack);
        if (fallingBack == _fallingBack)
            return;
        _fallingBack = fallingBack;
        SetStatus(fallingBack ? "Behind" : "Flank");
    }

    public override void ResetBehavior()
    {
        base.ResetBehavior();
        Wall = null;
    }
}
