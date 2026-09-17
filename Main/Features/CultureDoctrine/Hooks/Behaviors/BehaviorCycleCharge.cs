using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Behaviors;

/// <summary>
/// The cavalry cycle charge, driven by <see cref="CycleChargeMachine"/>: charge the closest
/// significant enemy formation (<c>ChargeToTarget</c>, so the riders fight), and once the
/// formation has passed through it, or has been in the melee for the cap, ride clear to a reform
/// point beyond the enemy, gather in a line facing it, and charge again. The engine's
/// <c>BehaviorTacticalCharge</c> carries this machine but short-circuits cavalry into a single
/// endless <c>ChargeToTarget</c> (`BehaviorTacticalCharge.cs:149-153`), which is the milling
/// melee a cavalry culture should not fight. Weight 1 for a cavalry or horse-archer formation
/// with an enemy, 0 otherwise; the navmesh penalty is disabled as vanilla does for charges.
/// </summary>
public sealed class BehaviorCycleCharge : TaomBehaviorBase
{
    private readonly CycleChargeMachine _machine = new CycleChargeMachine();
    private readonly CycleTunables _tunables;
    private float _stageStart;
    private Vec2 _chargeDirection = Vec2.Invalid;
    private float _stopDistance;
    private WorldPosition _reformPoint = WorldPosition.Invalid;
    private ChargeStage _arranged = ChargeStage.Reforming;

    public BehaviorCycleCharge(Formation formation)
        : this(formation, CycleTunables.Default)
    {
    }

    public BehaviorCycleCharge(Formation formation, CycleTunables tunables)
        : base(formation)
    {
        _tunables = tunables;
        BehaviorCoherence = 0.5f;
        CurrentOrder = MovementOrder.MovementOrderCharge;
        CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;
    }

    public ChargeStage Stage => _machine.Stage;

    public override float NavmeshlessTargetPositionPenalty => 1f;

    protected override float Weigh()
    {
        var q = Formation.QuerySystem;
        if (!q.IsCavalryFormation && !q.IsRangedCavalryFormation)
            return 0f;
        return Target() == null ? 0f : 1f;
    }

    protected override void Plan()
    {
        var target = Target();
        if (target == null)
        {
            CurrentOrder = MovementOrder.MovementOrderCharge;
            CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;
            return;
        }
        switch (_machine.Stage)
        {
            case ChargeStage.RidingThrough:
                CurrentOrder = MovementOrder.MovementOrderMove(_reformPoint.IsValid ? _reformPoint : ReformPoint(target.Formation));
                CurrentFacingOrder = FacingOrder.FacingOrderLookAtDirection(AwayFrom(target.Formation));
                break;
            case ChargeStage.Reforming:
                CurrentOrder = MovementOrder.MovementOrderMove(_reformPoint.IsValid ? _reformPoint : ReformPoint(target.Formation));
                CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;
                break;
            default:
                CurrentOrder = MovementOrder.MovementOrderChargeToTarget(target.Formation);
                CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;
                break;
        }
    }

    protected override void Activate()
    {
        _machine.Reset();
        _stageStart = Mission.Current.CurrentTime;
        _chargeDirection = Vec2.Invalid;
        _reformPoint = WorldPosition.Invalid;
        _arranged = ChargeStage.Reforming;
        Formation.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
        Formation.SetFormOrder(FormOrder.FormOrderWide);
        Arrange(ChargeStage.Charging);
        BeginCharge(Target());
        SetStatus(_machine.Stage.ToString());
    }

    protected override void OnActiveTick()
    {
        var formation = Formation;
        var target = Target();
        var now = Mission.Current.CurrentTime;
        if (target == null)
        {
            _machine.Reset();
            _stageStart = now;
            return;
        }
        var enemyPosition = target.Formation.CachedMedianPosition.AsVec2;
        var toEnemy = enemyPosition - formation.CachedAveragePosition;
        var distance = toEnemy.Length;
        var integrity = formation.CachedFormationIntegrityData;
        var reading = new ChargeReading(
            hasTarget: true,
            distanceToTarget: distance,
            stopDistance: _stopDistance,
            passedTarget: _chargeDirection.IsValid && _chargeDirection.DotProduct(toEnemy) <= 0f,
            gathered: integrity.DeviationOfPositionsExcludeFarAgents <= integrity.AverageMaxUnlimitedSpeedExcludeFarAgents * 0.5f,
            secondsInStage: now - _stageStart);
        if (!_machine.Step(in reading, in _tunables))
            return;
        _stageStart = now;
        switch (_machine.Stage)
        {
            case ChargeStage.Charging:
                BeginCharge(target);
                break;
            case ChargeStage.RidingThrough:
                _reformPoint = ReformPoint(target.Formation);
                break;
        }
        Arrange(_machine.Stage);
        SetStatus(_machine.Stage.ToString());
    }

    private void BeginCharge(FormationQuerySystem? target)
    {
        if (target == null)
            return;
        var toEnemy = target.Formation.CachedMedianPosition.AsVec2 - Formation.CachedAveragePosition;
        var distance = toEnemy.Normalize();
        _chargeDirection = distance > 1e-3f ? toEnemy : Vec2.Invalid;
        _stopDistance = _tunables.StopDistanceFor(distance);
        _reformPoint = WorldPosition.Invalid;
    }

    // Skein into the charge, a line to gather in; the ride-through keeps whatever it has.
    private void Arrange(ChargeStage stage)
    {
        if (stage == ChargeStage.RidingThrough || stage == _arranged)
            return;
        _arranged = stage;
        Formation.SetArrangementOrder(stage == ChargeStage.Charging ? ArrangementOrder.ArrangementOrderSkein : ArrangementOrder.ArrangementOrderLine);
    }

    // Beyond the enemy, on the side the riders are leaving by, at the charge's stop distance.
    private WorldPosition ReformPoint(Formation enemy)
    {
        var point = enemy.CachedMedianPosition;
        point.SetVec2(point.AsVec2 + AwayFrom(enemy) * _stopDistance);
        return point;
    }

    private Vec2 AwayFrom(Formation enemy)
    {
        var away = Formation.CachedAveragePosition - enemy.CachedMedianPosition.AsVec2;
        if (away.LengthSquared > 1e-4f)
            return away.Normalized();
        return _chargeDirection.IsValid ? _chargeDirection : Formation.Direction;
    }

    private FormationQuerySystem? Target() =>
        Formation.QuerySystem.ClosestSignificantlyLargeEnemyFormation ?? Formation.CachedClosestEnemyFormation;
}
