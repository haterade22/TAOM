using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Mumakil.BehaviorTreeElements;

/// <summary>
/// Engage gate for the Mûmakil attack branches: passes when a live enemy is within
/// <see cref="MumakilConfig.TrampleTriggerRange"/> in front of the beast and it is not already mid-attack. On a
/// passing scan it writes the best enemy's signed bearing to the blackboard so <see cref="MumakilSideAttackTask"/>
/// can pick the matching left/right swing. The pure decision is <see cref="IMumakilAttackService.ShouldEngage"/>;
/// this is boundary code (touches Mission/Agent directly). 1-for-1 with the elephant's engage decorator.
/// </summary>
public class MumakilEngageDecorator : BTReturnFalseDecorator, IBTBannerlordBase, IBTMumakilBlackboard
{
    private readonly MBList<Agent> _scratch = new();
    private IMumakilAttackService _service;

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
        Agent mumakil = Agent.GetValue();
        if (mumakil == null || !mumakil.IsActive()) return false;
        Agent rider = mumakil.RiderAgent;
        if (rider == null) return false;                                            // gated upstream; defensive
        if (!mumakil.ActionSet.IsValid) return false;

        // Index comparison against our own attack caches — zero-alloc (no per-eval native GetName() marshal)
        // and immune to other action names containing "attack".
        bool alreadyAttacking = MumakilAttackActions.IsMumakilAttack(mumakil.GetCurrentAction(0));
        if (alreadyAttacking) return false;                                          // cheap exit before the scan

        // One scan at the (larger) damage radius; the gate uses the BEST-facing enemy within the trigger range.
        Mission.Current.GetNearbyAgents(mumakil.Position.AsVec2, MumakilConfig.TrampleRadius, _scratch);
        Vec3 lookDir = mumakil.LookDirection;
        float bestFacingDot = -1f;
        float bestBearing = 0f;
        foreach (Agent a in _scratch)
        {
            if (a == null || a == mumakil || !a.IsActive() || !a.IsEnemyOf(rider)) continue;
            Vec3 offset = a.Position - mumakil.Position;
            if (offset.Length > MumakilConfig.TrampleTriggerRange) continue;
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

        _service ??= IoC.Resolve<IMumakilAttackService>();
        if (!_service.ShouldEngage(bestFacingDot, alreadyAttacking: false)) return false;

        TargetBearing.SetValue(bestBearing);
        return true;
    }
}
