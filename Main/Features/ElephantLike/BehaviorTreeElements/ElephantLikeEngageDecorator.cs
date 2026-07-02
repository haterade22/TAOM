using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.ElephantLike.BehaviorTreeElements;

/// <summary>
/// Engage gate for the elephant-like attack branches: passes when a live enemy is within the profile's
/// <see cref="ElephantLikeCombatProfile.TrampleTriggerRange"/> in front of the creature and the creature is not
/// already mid-attack. On a passing scan it writes the best enemy's signed bearing to the blackboard so
/// <see cref="ElephantLikeSideAttackTask"/> can pick the matching left/right swing. The pure decision is
/// <see cref="IElephantLikeAttackService.ShouldEngage"/>; this is boundary code (touches Mission/Agent directly),
/// like the warg's target-scan decorators. The 2026-06-10 cooldown rework removed the upstream pack's per-tick
/// probability roll — the AttackOffCooldownDecorator branches downstream are the rate limiter now, and the scan
/// cadence is bounded by the idle SleepTask sibling in the tree (warg's NoEnemyCloseDecorator economics).
/// </summary>
public class ElephantLikeEngageDecorator : BTReturnFalseDecorator, IBTBannerlordBase, IBTElephantLikeBlackboard
{
    private readonly MBList<Agent> _scratch = new();
    private readonly ElephantLikeCombatProfile _profile;
    private IElephantLikeAttackService _service;

    private BTBlackboardValue<Agent> _agent;
    private BTBlackboardValue<System.DateTime?> _trampleLastFired;
    private BTBlackboardValue<System.DateTime?> _sideAttackLastFired;
    private BTBlackboardValue<float> _targetBearing;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public BTBlackboardValue<System.DateTime?> TrampleLastFired { get => _trampleLastFired; set => _trampleLastFired = value; }
    public BTBlackboardValue<System.DateTime?> SideAttackLastFired { get => _sideAttackLastFired; set => _sideAttackLastFired = value; }
    public BTBlackboardValue<float> TargetBearing { get => _targetBearing; set => _targetBearing = value; }

    public ElephantLikeEngageDecorator(ElephantLikeCombatProfile profile) => _profile = profile;

    public override bool Evaluate()
    {
        Agent creature = Agent.GetValue();
        if (creature == null || !creature.IsActive()) return false;
        Agent rider = creature.RiderAgent;
        if (rider == null) return false;                                            // gated upstream; defensive
        if (!creature.ActionSet.IsValid) return false;

        // Index comparison against our own attack caches — zero-alloc (no per-eval native GetName() marshal)
        // and immune to other action names containing "attack" (review finding 2026-06-10).
        bool alreadyAttacking = _profile.IsAttack(creature.GetCurrentAction(0));
        if (alreadyAttacking) return false;                                          // cheap exit before the scan

        // One scan at the (larger) damage radius; the gate uses the BEST-facing enemy within the trigger range.
        Mission.Current.GetNearbyAgents(creature.Position.AsVec2, _profile.TrampleRadius, _scratch);
        Vec3 lookDir = creature.LookDirection;
        float bestFacingDot = -1f;
        float bestBearing = 0f;
        foreach (Agent a in _scratch)
        {
            if (a == null || a == creature || !a.IsActive() || !a.IsEnemyOf(rider)) continue;
            Vec3 offset = a.Position - creature.Position;
            if (offset.Length > _profile.TrampleTriggerRange) continue;
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

        _service ??= _profile.ResolveService();
        if (!_service.ShouldEngage(bestFacingDot, alreadyAttacking: false)) return false;

        TargetBearing.SetValue(bestBearing);
        return true;
    }
}
