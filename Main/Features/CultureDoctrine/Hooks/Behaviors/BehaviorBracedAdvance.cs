using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Behaviors;

/// <summary>
/// A wall on the march that braces against horse: <c>BehaviorAdvance</c>
/// (`BehaviorAdvance.cs`) with the anti-cavalry square from <see cref="BraceDecision"/>. Walks
/// in a line at the enemy's main body (its median plus half its depth, as vanilla), closes the
/// shields inside vanilla's 10 to 80 m band under fire, and goes Square and stands while a
/// horse formation rides at it inside <c>CavalryMattersMetres</c> or did in the last few seconds
/// (<see cref="EnemyScan"/>); vanilla only stops and re-forms five metres on when the horse is
/// 30 m out. The main body it walks at is the nearest enemy FOOT formation
/// (<see cref="TargetSelection"/>), never a passing eored. Weight 1, as <c>BehaviorAdvance</c>.
/// </summary>
public sealed class BehaviorBracedAdvance : TaomBehaviorBase
{
    // BehaviorAdvance's shield band: enter under 80 m and over 10 m, leave over 100 m or under 5 m.
    private const float ShieldBandFarSquared = 6400f;
    private const float ShieldBandFarHysteresisSquared = 3600f;
    private const float ShieldBandNearSquared = 100f;
    private const float ShieldBandNearHysteresisSquared = 75f;

    private float _lastChargeSignal = -1f;
    private WallStance _stance = WallStance.Line;
    private bool _braced;
    private bool _inShieldBand;
    private WorldPosition _bracePoint = WorldPosition.Invalid;
    private Formation? _target;

    public BehaviorBracedAdvance(Formation formation)
        : base(formation)
    {
        BehaviorCoherence = 0.8f;
        CalculateCurrentOrder();
    }

    protected override float Weigh() => 1f;

    protected override void Plan()
    {
        var formation = Formation;
        if (_braced)
        {
            // Stand on the point taken when the brace began, not the drifting average.
            CurrentOrder = MovementOrder.MovementOrderMove(_bracePoint.IsValid ? _bracePoint : WallStances.Here(formation));
            CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;
            return;
        }
        var enemy = _target != null && _target.CountOfUnits > 0 ? _target : formation.QuerySystem.Team.MedianTargetFormation?.Formation;
        if (enemy == null)
        {
            CurrentOrder = MovementOrder.MovementOrderMove(WallStances.Here(formation));
            CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;
            return;
        }
        var position = enemy.CachedMedianPosition;
        position.SetVec2(position.AsVec2 + enemy.Direction * enemy.Depth * 0.5f);
        var toEnemy = position.AsVec2 - formation.CachedAveragePosition;
        CurrentOrder = MovementOrder.MovementOrderMove(position);
        CurrentFacingOrder = toEnemy.LengthSquared > 1e-4f
            ? FacingOrder.FacingOrderLookAtDirection(toEnemy.Normalized())
            : FacingOrder.FacingOrderLookAtEnemy;
    }

    protected override void Activate()
    {
        _lastChargeSignal = -1f;
        _braced = false;
        _bracePoint = WorldPosition.Invalid;
        _inShieldBand = false;
        _target = null;
        _stance = WallStance.Line;
        Formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderLine);
        Formation.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
        Formation.SetFormOrder(FormOrder.FormOrderWide);
        SetStatus(_stance.ToString());
    }

    protected override void OnActiveTick()
    {
        var formation = Formation;
        var q = formation.QuerySystem;
        _target = EnemyScan.Pick(formation, Targets, in Engagement, _braced, out var cavalryThreat);
        var braced = WallStances.Braced(cavalryThreat, ref _lastChargeSignal);
        if (braced && !_braced)
            _bracePoint = WallStances.Here(formation);
        else if (!braced)
            _bracePoint = WorldPosition.Invalid;
        _braced = braced;
        var enemy = _target;
        if (enemy != null)
        {
            var d2 = WallStances.DistanceSquaredTo(formation, enemy.CachedMedianPosition.AsVec2);
            _inShieldBand = d2 < ShieldBandFarSquared + (_inShieldBand ? ShieldBandFarHysteresisSquared : 0f)
                && d2 > ShieldBandNearSquared - (_inShieldBand ? ShieldBandNearHysteresisSquared : 0f);
        }
        else
        {
            _inShieldBand = false;
        }
        var situation = new WallSituation(
            atPosition: false,
            hasShield: q.HasShield,
            cavalryChargeInbound: _braced,
            underRangedAttack: enemy != null && q.IsUnderRangedAttack,
            enemyAtArmsLength: false,
            withinShieldDistance: _inShieldBand);
        var stance = BraceDecision.Advance(in situation, _braced);
        if (stance != _stance)
        {
            _stance = stance;
            formation.SetArrangementOrder(WallStances.Arrangement(stance));
            SetStatus(stance.ToString());
        }
    }
}
