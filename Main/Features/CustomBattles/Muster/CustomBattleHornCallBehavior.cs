using System.Collections.Generic;
using System.Linq;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CustomBattles.Muster;

/// <summary>
/// A mustered unit's horn blower sounds his horn whenever his formation gets a new order to go somewhere: charge,
/// advance, follow, or a move to a new place (not on the order he starts with, and not while he still plays). He keeps
/// marching: weapons away, the cornu clip on the upper-body channel, the horn prefab (Prefabs/tcb_horn.xml) on his
/// head bone. His horn action set (ModuleData/action_sets.xml) is given once, while he still stands at the start.
/// Every held agent handle passes AgentSlotIdentity first (the engine recycles a dead agent's slot).
/// Boundary code (raw Agent); game-tested per ADR-008.
/// </summary>
internal sealed class CustomBattleHornCallBehavior : MissionBehavior
{
    private const float CheckInterval = 0.25f;
    private const float MoveTargetChangeSq = 25f;

    private readonly CustomBattleRolesBehavior _roles;
    private readonly Dictionary<Agent, HornBlower> _blowers = new Dictionary<Agent, HornBlower>();
    private float _checkAt;

    public CustomBattleHornCallBehavior(CustomBattleRolesBehavior roles) => _roles = roles;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public override void OnMissionTick(float dt)
    {
        var mission = Mission;
        if (mission == null) return;
        float now = mission.CurrentTime;
        foreach (var a in _roles.HornBlowers)
        {
            if (!AgentSlotIdentity.IsCurrentOccupant(a) || !a.IsActive()) continue;
            var b = Get(a);
            if (b.Equipped) continue;
            b.Equipped = true;
            HornBlowerActions.GiveHornSet(b);
        }
        foreach (var b in _blowers.Values.ToList())
        {
            if (!AgentSlotIdentity.IsCurrentOccupant(b.Agent)) { _blowers.Remove(b.Agent); continue; }
            if (b.Until > 0 && now >= b.Until) HornBlowerActions.Stop(b);
        }
        if (!mission.IsDeploymentFinished || now < _checkAt) return;
        _checkAt = now + CheckInterval;
        foreach (var a in _roles.HornBlowers)
        {
            if (!AgentSlotIdentity.IsCurrentOccupant(a) || !a.IsActive()) continue;
            var b = Get(a);
            var f = a.Formation;
            var order = f?.GetReadonlyMovementOrderReference().OrderEnum ?? MovementOrder.MovementOrderEnum.Invalid;
            var target = f?.OrderPosition ?? Vec2.Zero;
            bool goes = order == MovementOrder.MovementOrderEnum.Charge || order == MovementOrder.MovementOrderEnum.ChargeToTarget
                        || order == MovementOrder.MovementOrderEnum.Advance || order == MovementOrder.MovementOrderEnum.Follow
                        || order == MovementOrder.MovementOrderEnum.Move;
            bool isNew = order != b.LastOrder
                         || (order == MovementOrder.MovementOrderEnum.Move && target.DistanceSquared(b.LastTarget) > MoveTargetChangeSq);
            bool first = !b.Seen;
            b.Seen = true;
            b.LastOrder = order;
            b.LastTarget = target;
            if (goes && isNew && !first && b.Until < 0)
                HornBlowerActions.Blow(mission, b, order.ToString().ToLowerInvariant());
        }
    }

    public override void OnRemoveBehavior()
    {
        base.OnRemoveBehavior();
        _blowers.Clear();
    }

    private HornBlower Get(Agent a)
    {
        if (!_blowers.TryGetValue(a, out var b)) _blowers[a] = b = new HornBlower(a);
        return b;
    }
}
