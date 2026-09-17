using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Behaviors;

/// <summary>
/// A foot formation's charge: <c>BehaviorCharge</c> (`BehaviorCharge.cs`) with the target chosen
/// by <see cref="TargetSelection"/> instead of <c>CachedClosestEnemyFormation</c>, so the line
/// runs at the nearest enemy foot formation and never after horse it cannot catch; horse riding
/// at it inside <c>CavalryMattersMetres</c> stop it where it stands in a square until they are
/// past (<see cref="BraceDecision"/>), then it charges on. With no foot left it charges the
/// nearest horse, and with nothing known at all it charges as vanilla does. Replaces
/// <c>BehaviorCharge</c> and <c>BehaviorTacticalCharge</c> in every foot row of the TAOM plans,
/// and weighs as <c>BehaviorTacticalCharge</c> does for infantry (<see cref="ChargeWeight"/>,
/// against its own target) so the rows keep their meaning: 0.8 far out, up to 1.44 close
/// against a target looking elsewhere, half of that against horse.
/// </summary>
public sealed class BehaviorFootCharge : TaomBehaviorBase
{
    public override float NavmeshlessTargetPositionPenalty => 1f;

    private float _lastChargeSignal = -1f;
    private bool _braced;
    private Formation? _target;

    public BehaviorFootCharge(Formation formation)
        : base(formation)
    {
        BehaviorCoherence = 0.5f;
        CalculateCurrentOrder();
    }

    protected override float Weigh()
    {
        var formation = Formation;
        _target = EnemyScan.Pick(formation, Targets, in Engagement, _braced, out _);
        if (_target == null)
            return formation.Team.HasAnyEnemyTeamsWithAgents(false) ? 0.2f : 0f;
        var q = _target.QuerySystem;
        var seconds = formation.CachedAveragePosition.Distance(_target.CachedMedianPosition.AsVec2) / formation.QuerySystem.MovementSpeedMaximum;
        return ChargeWeight.Foot(seconds,
            targetNotOnUs: _target.CachedClosestEnemyFormation != formation.QuerySystem,
            targetIsHorse: q.IsCavalryFormation || q.IsRangedCavalryFormation);
    }

    protected override void Plan()
    {
        var formation = Formation;
        if (_braced)
        {
            CurrentOrder = MovementOrder.MovementOrderMove(WallStances.Here(formation));
            CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;
            return;
        }
        CurrentOrder = _target != null && _target.CountOfUnits > 0
            ? MovementOrder.MovementOrderChargeToTarget(_target)
            : MovementOrder.MovementOrderCharge;
        CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;
    }

    protected override void Activate()
    {
        _lastChargeSignal = -1f;
        _braced = false;
        _target = null;
        Formation.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
        if (Formation.ArrangementOrder.OrderEnum == ArrangementOrder.ArrangementOrderEnum.ShieldWall)
            Formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderLine);
        SetStatus("Charging");
    }

    protected override void OnActiveTick()
    {
        var formation = Formation;
        _target = EnemyScan.Pick(formation, Targets, in Engagement, _braced, out var cavalryThreat);
        var braced = WallStances.Braced(cavalryThreat, ref _lastChargeSignal);
        if (braced == _braced)
            return;
        _braced = braced;
        formation.SetArrangementOrder(braced ? ArrangementOrder.ArrangementOrderSquare : ArrangementOrder.ArrangementOrderLine);
        SetStatus(braced ? "Square" : "Charging");
    }
}
