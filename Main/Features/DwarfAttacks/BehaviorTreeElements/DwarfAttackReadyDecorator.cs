using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DwarfAttacks.BehaviorTreeElements;

/// <summary>
/// Passes when the dwarf may start an attack, and writes which one to <see cref="Chosen"/>: the feature is on, the
/// dwarf is not the player's own, on foot and not using a machine, at least one attack resolved and is off cooldown,
/// he is not already attacking, being struck or falling, and his nearest enemy on foot and on his level is in one of
/// the service's bands and in front. The decision is the service's; this node only reads the engine. It must not throw: a throw stops the whole tree for the battle
/// (BehaviorTreesCore.RunTree).
/// </summary>
public class DwarfAttackReadyDecorator : BTReturnFalseDecorator, IBTBannerlordBase, IBTDwarfAttacksBlackboard
{
    private readonly MBList<Agent> _scratch = new();
    private readonly DwarfAttackService _service;
    private readonly DwarfAttacksSettingsProvider _settings;

    private BTBlackboardValue<Agent> _agent;
    private BTBlackboardValue<float?> _headButtLastFired;
    private BTBlackboardValue<float?> _dropKickLastFired;
    private BTBlackboardValue<DwarfAttack?> _chosen;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public BTBlackboardValue<float?> HeadButtLastFired { get => _headButtLastFired; set => _headButtLastFired = value; }
    public BTBlackboardValue<float?> DropKickLastFired { get => _dropKickLastFired; set => _dropKickLastFired = value; }
    public BTBlackboardValue<DwarfAttack?> Chosen { get => _chosen; set => _chosen = value; }

    public DwarfAttackReadyDecorator(DwarfAttackService service, DwarfAttacksSettingsProvider settings)
    {
        _service = service;
        _settings = settings;
    }

    public override bool Evaluate()
    {
        Agent dwarf = Agent.GetValue();
        Mission mission = Mission.Current;
        // The tree holds this handle across frames; a recycled slot answers for its new tenant (#592). The identity
        // check comes first: IsActive() is already a native read.
        if (dwarf == null || mission == null || !AgentSlotIdentity.IsCurrentOccupant(dwarf) || !dwarf.IsActive())
            return false;

        if (!_settings.Enabled || dwarf.Team == null) return false;
        // IsAiAgentDecorator reads only the controller, so the player's own dwarf is excluded by who he is.
        // IsPlayerTroop survives MainAgent = null.
        if (!_service.MayAttack(dwarf.IsMainAgent || dwarf.IsPlayerTroop, dwarf.HasMount, dwarf.IsUsingGameObject))
            return false;

        float now = mission.CurrentTime;
        bool headButtReady = _service.IsReady(DwarfAttack.HeadButt, DwarfAttacksCombat.IsResolved(DwarfAttack.HeadButt),
            HeadButtLastFired.GetValue(), now);
        bool dropKickReady = _service.IsReady(DwarfAttack.DropKick, DwarfAttacksCombat.IsResolved(DwarfAttack.DropKick),
            DropKickLastFired.GetValue(), now);
        if (!headButtReady && !dropKickReady) return false;

        if (DwarfAttacksCombat.IsOurs(dwarf.GetCurrentAction(0))
            || dwarf.IsInBeingStruckAction
            || dwarf.GetCurrentActionType(0) == TaleWorlds.MountAndBlade.Agent.ActionCodeType.Fall)
            return false;

        // The nearest enemy on foot and on the dwarf's level decides: a drop kick past a closer enemy, or a butt at one
        // behind him, would look wrong. The engine's query is flat, so the height test is ours.
        Vec3 position3 = dwarf.Position;
        Vec2 position = position3.AsVec2;
        _scratch.Clear();
        mission.GetNearbyEnemyAgents(position, DwarfAttacksConfig.DropKickMaxRange, dwarf.Team, _scratch);
        Agent? nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Agent other in _scratch)
        {
            if (!DwarfAttacksCombat.IsCandidate(dwarf, other)) continue;
            Vec3 otherPosition = other.Position;
            if (!_service.IsWithinHeight(otherPosition.z - position3.z)) continue;
            float distance = (otherPosition.AsVec2 - position).Length;
            if (distance > 0f && distance < nearestDistance)
            {
                nearest = other;
                nearestDistance = distance;
            }
        }
        if (nearest == null) return false;

        Vec2 look = dwarf.LookDirection.AsVec2;
        float lookLength = look.Length;
        if (!(lookLength > 1e-4f)) return false;
        Vec2 offset = nearest.Position.AsVec2 - position;
        float facing = (offset.x * look.x + offset.y * look.y) / (nearestDistance * lookLength);

        DwarfAttack? choice = _service.ChooseAttack(nearestDistance, facing, headButtReady, dropKickReady);
        if (choice == null) return false;
        Chosen.SetValue(choice);
        return true;
    }
}
