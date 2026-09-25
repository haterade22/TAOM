using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.TrollBruteForce.BehaviorTreeElements;

/// <summary>
/// Passes when the troll may start Brute Force: off cooldown (mission time), not already smashing, not being
/// struck or falling, and an enemy human within the scaled trigger range and in front. The decision is the
/// service's; this node only reads the engine. It must not throw: a throw stops the whole tree for the battle
/// (BehaviorTreesCore.RunTree).
/// </summary>
public class BruteForceReadyDecorator : BTReturnFalseDecorator, IBTBannerlordBase, IBTTrollBruteForceBlackboard
{
    private readonly MBList<Agent> _scratch = new();
    private ITrollBruteForceService? _service;

    private BTBlackboardValue<Agent> _agent;
    private BTBlackboardValue<float?> _lastFired;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public BTBlackboardValue<float?> LastFired { get => _lastFired; set => _lastFired = value; }

    public override bool Evaluate()
    {
        Agent troll = Agent.GetValue();
        Mission mission = Mission.Current;
        // The tree holds this handle across frames; a recycled slot answers for its new tenant (#592).
        if (troll == null || mission == null || !troll.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(troll))
            return false;

        _service ??= IoC.Resolve<ITrollBruteForceService>();
        if (!_service.IsOffCooldown(LastFired.GetValue(), mission.CurrentTime)) return false;

        bool busy = troll.GetCurrentAction(0) == TrollBruteForceCombat.Action
            || troll.IsInBeingStruckAction
            || troll.GetCurrentActionType(0) == TaleWorlds.MountAndBlade.Agent.ActionCodeType.Fall;
        if (busy) return false;

        float scale = troll.AgentScale;
        Vec3 position = troll.Position;
        Vec3 look = troll.LookDirection;

        // Any one enemy in reach and in front is enough; each is judged by the service's rule, so a nearer
        // enemy slightly off-centre is not hidden behind a more central one standing out of reach.
        _scratch.Clear();
        mission.GetNearbyAgents(position.AsVec2, _service.OuterRadius(scale), _scratch);
        foreach (Agent other in _scratch)
        {
            if (other == null || other == troll || !other.IsActive() || other.IsMount || !other.IsEnemyOf(troll))
                continue;

            Vec3 offset = other.Position - position;
            float distance = offset.Length;
            if (!(distance > 0f)) continue;
            float facing = Vec3.DotProduct(offset.NormalizedCopy(), look);
            if (_service.ShouldEngage(distance, facing, scale, busy: false)) return true;
        }

        return false;
    }
}
