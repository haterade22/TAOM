using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Behaviors;

/// <summary>
/// One wing of an envelopment: an infantry block that marches in a deep line to a point beside
/// the enemy's main body on its own side of the axis from the centre (<see cref="EnvelopGeometry"/>),
/// then charges it from the flank. The engine's <c>BehaviorFlank</c> goes to the team's flank
/// edge but weighs 0 whenever the enemy's closest formation is the flanker itself
/// (`BehaviorFlank.cs:51-64`), which for infantry wings is most of the march. Weight 1 with an
/// enemy in view, 0 without. The side comes from <c>Formation.AI.Side</c>, which the tactic sets
/// when it assigns the wings.
/// </summary>
public sealed class BehaviorEnvelopWing : TaomBehaviorBase
{
    private bool _charging;

    public BehaviorEnvelopWing(Formation formation)
        : base(formation)
    {
        BehaviorCoherence = 0.6f;
        CalculateCurrentOrder();
    }

    public bool IsCharging => _charging;

    // Weighed on every formation tick, so the cheap engine question (is there any enemy)
    // rather than the scan; the scan runs on the active tick and the plan.
    protected override float Weigh() => Formation.CachedClosestEnemyFormation == null ? 0f : 1f;

    protected override void Plan()
    {
        var formation = Formation;
        var target = Target();
        if (target == null)
        {
            CurrentOrder = MovementOrder.MovementOrderMove(WallStances.Here(formation));
            CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;
            return;
        }
        var enemy = target;
        if (_charging)
        {
            CurrentOrder = MovementOrder.MovementOrderChargeToTarget(enemy);
            CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;
            return;
        }
        var centre = formation.QuerySystem.MainFormation?.CachedAveragePosition ?? formation.QuerySystem.Team.AveragePosition;
        var point = EnvelopGeometry.WingPoint(centre, enemy.CachedMedianPosition.AsVec2, enemy.Width, formation.Width, Side(), EnvelopGeometry.DefaultMargin);
        var position = enemy.CachedMedianPosition;
        position.SetVec2(point);
        var toEnemy = enemy.CachedMedianPosition.AsVec2 - formation.CachedAveragePosition;
        CurrentOrder = MovementOrder.MovementOrderMove(position);
        CurrentFacingOrder = toEnemy.LengthSquared > 1e-4f ? FacingOrder.FacingOrderLookAtDirection(toEnemy.Normalized()) : FacingOrder.FacingOrderLookAtEnemy;
    }

    protected override void Activate()
    {
        _charging = false;
        _target = null;
        Formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderLine);
        Formation.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
        Formation.SetFormOrder(FormOrder.FormOrderDeep);
        SetStatus("Marching");
    }

    protected override void OnActiveTick()
    {
        _target = EnemyScan.Pick(Formation, Targets, in Engagement, false, out _);
        if (_charging)
            return;
        var formation = Formation;
        var target = Target();
        if (target == null)
            return;
        var enemyPosition = target.CachedMedianPosition.AsVec2;
        var toPoint = CurrentOrder.GetPosition(formation).Distance(formation.CachedAveragePosition);
        var toEnemy = enemyPosition.Distance(formation.CachedAveragePosition);
        if (EnvelopGeometry.ShouldCharge(toPoint, toEnemy))
        {
            _charging = true;
            SetStatus("Charging");
        }
    }

    private WingSide Side() => Formation.AI.Side == FormationAI.BehaviorSide.Left ? WingSide.Left : WingSide.Right;

    // The nearest enemy foot formation (TargetSelection), scanned once per active tick; a wing
    // never swings round horse, and it does not brace: it is the flank, not the wall.
    private Formation? _target;

    private Formation? Target() => _target != null && _target.CountOfUnits > 0 ? _target : null;
}
