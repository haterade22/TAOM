using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Elephant.BehaviorTreeElements;

/// <summary>
/// Engage gate for the elephant attack branches: passes when a live enemy is within
/// <see cref="ElephantConfig.TrampleTriggerRange"/> in front of the elephant's tusks and the elephant is not
/// already mid-attack. On a passing scan it writes the best enemy's signed bearing to the blackboard so
/// <see cref="ElephantSideAttackTask"/> can pick the matching left/right swing. The pure decision is
/// <see cref="IElephantAttackService.ShouldEngage"/>; this is boundary code (touches Mission/Agent directly),
/// like the warg's target-scan decorators. The 2026-06-10 cooldown rework removed ADOD's per-tick probability
/// roll — the AttackOffCooldownDecorator branches downstream are the rate limiter now, and the scan cadence is
/// bounded by the idle SleepTask sibling in the tree (warg's NoEnemyCloseDecorator economics).
/// </summary>
public class EnemyInTrampleRangeDecorator : BTReturnFalseDecorator, IBTBannerlordBase, IBTElephantBlackboard
{
    private readonly MBList<Agent> _scratch = new();
    private IElephantAttackService _service;

    private BTBlackboardValue<Agent> _agent;
    private BTBlackboardValue<System.DateTime?> _trampleLastFired;
    private BTBlackboardValue<System.DateTime?> _sideAttackLastFired;
    private BTBlackboardValue<float> _targetBearing;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public BTBlackboardValue<System.DateTime?> TrampleLastFired { get => _trampleLastFired; set => _trampleLastFired = value; }
    public BTBlackboardValue<System.DateTime?> SideAttackLastFired { get => _sideAttackLastFired; set => _sideAttackLastFired = value; }
    public BTBlackboardValue<float> TargetBearing { get => _targetBearing; set => _targetBearing = value; }

    public override bool Evaluate()
    {
        Agent elephant = Agent.GetValue();
        if (elephant == null || !elephant.IsActive()) return false;
        Agent rider = elephant.RiderAgent;
        if (rider == null) return false;                                            // gated upstream; defensive
        if (!elephant.ActionSet.IsValid) return false;

        // Index comparison against our own attack caches — zero-alloc (no per-eval native GetName() marshal)
        // and immune to other action names containing "attack" (review finding 2026-06-10).
        bool alreadyAttacking = ElephantAttackActions.IsElephantAttack(elephant.GetCurrentAction(0));
        if (alreadyAttacking) return false;                                          // cheap exit before the scan

        // One scan at the (larger) damage radius; the gate uses the BEST-facing enemy within the trigger range.
        Mission.Current.GetNearbyAgents(elephant.Position.AsVec2, ElephantConfig.TrampleRadius, _scratch);
        Vec3 lookDir = elephant.LookDirection;
        float bestFacingDot = -1f;
        float bestBearing = 0f;
        foreach (Agent a in _scratch)
        {
            if (a == null || a == elephant || !a.IsActive() || !a.IsEnemyOf(rider)) continue;
            Vec3 offset = a.Position - elephant.Position;
            if (offset.Length > ElephantConfig.TrampleTriggerRange) continue;
            Vec3 toEnemy = offset.NormalizedCopy();
            float dot = Vec3.DotProduct(toEnemy, lookDir);
            if (dot > bestFacingDot)
            {
                bestFacingDot = dot;
                // Signed bearing: z of cross(lookDir, toEnemy). POSITIVE = enemy on the LEFT
                // (counter-clockwise, Z-up right-handed), NEGATIVE = RIGHT.
                bestBearing = lookDir.x * toEnemy.y - lookDir.y * toEnemy.x;
            }
        }

        _service ??= IoC.Resolve<IElephantAttackService>();
        if (!_service.ShouldEngage(bestFacingDot, alreadyAttacking: false)) return false;

        TargetBearing.SetValue(bestBearing);
        return true;
    }
}
