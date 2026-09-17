using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Behaviors;

/// <summary>
/// Hit-and-run for throwing infantry, driven by <see cref="InfantrySkirmishMachine"/>: close to
/// javelin range, throw, fall back from foot that comes within 40% of the range, and once the
/// javelins are spent weigh 0 so the plan's melee rows take the line. Positions and thresholds
/// are <c>BehaviorSkirmish</c>'s (`BehaviorSkirmish.cs:39-222`); the weight keys on the
/// formation's throwing share instead of its ranged class share, which is 0 for a javelin line.
/// </summary>
public sealed class BehaviorInfantrySkirmish : TaomBehaviorBase
{
    private const float PullBackClearance = 10f;

    private readonly InfantrySkirmishMachine _machine = new InfantrySkirmishMachine();

    public BehaviorInfantrySkirmish(Formation formation)
        : base(formation)
    {
        BehaviorCoherence = 0.5f;
        CalculateCurrentOrder();
    }

    public SkirmishStage Stage => _machine.Stage;

    protected override float Weigh()
    {
        if (_machine.Stage == SkirmishStage.Committed)
            return 0f;
        var throwing = Formation.QuerySystem.HasThrowingUnitRatio;
        var share = throwing < 0f ? 0f : throwing > 0.5f ? 0.5f : throwing;
        return 0.1f + 0.9f * share * 2f;
    }

    protected override void Plan()
    {
        var formation = Formation;
        var enemy = formation.QuerySystem.ClosestSignificantlyLargeEnemyFormation;
        var position = formation.CachedMedianPosition;
        Vec2 facing;
        if (enemy == null)
        {
            facing = formation.Direction;
            position.SetVec2(formation.CachedAveragePosition);
        }
        else
        {
            var toEnemy = enemy.Formation.CachedMedianPosition.AsVec2 - formation.CachedAveragePosition;
            facing = toEnemy.LengthSquared > 1e-4f ? toEnemy.Normalized() : formation.Direction;
            switch (_machine.Stage)
            {
                case SkirmishStage.Approaching:
                    position = enemy.Formation.CachedMedianPosition;
                    position.SetVec2(enemy.Formation.CachedAveragePosition);
                    break;
                case SkirmishStage.PullingBack:
                    position = enemy.Formation.CachedMedianPosition;
                    position.SetVec2(position.AsVec2 - facing * (formation.QuerySystem.MissileRangeAdjusted - formation.Depth * 0.5f - PullBackClearance));
                    break;
                default:
                    // Throwing (and Committed, which never runs): hold, half a depth along the
                    // current motion so a moving line settles rather than snaps back.
                    position.SetVec2(formation.CachedAveragePosition + formation.CachedCurrentVelocity.Normalized() * (formation.Depth * 0.5f));
                    break;
            }
        }
        CurrentOrder = MovementOrder.MovementOrderMove(position);
        CurrentFacingOrder = FacingOrder.FacingOrderLookAtDirection(facing);
    }

    protected override void Activate()
    {
        // A spent line stays spent: javelins do not come back between activations, and a
        // committed machine weighs 0, so this only runs when the plan re-armed the row by hand.
        if (_machine.Stage != SkirmishStage.Committed)
            _machine.Reset(Mission.Current.CurrentTime);
        Formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderLoose);
        Formation.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
        Formation.SetFormOrder(FormOrder.FormOrderWide);
        SetStatus(_machine.Stage.ToString());
    }

    protected override void OnActiveTick()
    {
        var formation = Formation;
        var q = formation.QuerySystem;
        var enemy = q.ClosestSignificantlyLargeEnemyFormation;
        var distance = float.MaxValue;
        var enemyIsInfantry = false;
        if (enemy != null)
        {
            // BehaviorSkirmish's closing term: where the enemy will be 5 to 10 s from now.
            var toEnemy = enemy.Formation.CachedMedianPosition.AsVec2 - formation.CachedAveragePosition;
            distance = toEnemy.Normalize();
            var closing = enemy.Formation.CachedCurrentVelocity.DotProduct(toEnemy);
            var count = formation.CountOfUnits < 10 ? 10f : formation.CountOfUnits > 60 ? 60f : formation.CountOfUnits;
            distance += (5f + 5f * (count - 10f) * 0.02f) * closing;
            enemyIsInfantry = enemy.IsInfantryFormation;
        }
        var reading = new SkirmishReading(
            hasEnemy: enemy != null, distance: distance, maximumRange: q.MaximumMissileRange, rangeAdjusted: q.MissileRangeAdjusted,
            makingRangedAttackRatio: q.MakingRangedAttackRatio, throwingRatio: q.HasThrowingUnitRatio,
            unitCount: formation.CountOfUnits, enemyIsInfantry: enemyIsInfantry, now: Mission.Current.CurrentTime);
        if (_machine.Step(in reading))
            SetStatus(_machine.Stage.ToString());
    }

    // ResetBehavior runs on every plan apply (ResetBehaviorWeights); the stage is kept so a
    // formation-set change elsewhere on the team does not re-arm a line with no javelins.
    public override void ResetBehavior()
    {
        base.ResetBehavior();
    }
}
