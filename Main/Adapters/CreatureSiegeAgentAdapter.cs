using System;
using TAOM.Features.CreatureSiegeRole.Domain;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// One creature agent for one reconcile pass, over the engine's per-agent primitives (v1.5.4): the scripted position and attack
/// target, the navmesh face exclusion, the scripted-flag reads, the path query. The mission adapter builds a fresh one for every
/// creature from the live agent list, on the main thread, from the mission tick (never from the engine's AI thread), so an
/// instance is never held across frames and needs no slot-identity check: an agent in the active list owns its slot
/// (csharp-architecture.md, "Mission-scope agent handles"). Members referenced here are pinned by the binding tests, because a
/// member that stops resolving fails when its method is first compiled, before any try around the call can run.
///
/// The scripted movement is vanilla's own for a gate: a scripted position with the consider-rotation flag, and an attack-entity
/// target with no extra flags (<c>AttackEntityOrderSecondaryDetachment</c>). Native stores the scripted flags as the flag passed
/// or'd with 5, so they are never exactly "go to position" whichever flag is passed (the consider-rotation flag is not what
/// ensures it), and a ladder queue drops an agent it holds.
/// </summary>
public sealed class CreatureSiegeAgentAdapter : ICreatureSiegeAgentAdapter
{
    private readonly Agent _agent;
    private readonly Scene _scene;

    public CreatureSiegeAgentAdapter(Agent agent, Scene scene)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
    }

    public object Identity => _agent;

    public bool IsAIControlled => _agent.IsAIControlled;

    public bool IsFleeing => _agent.IsRunningAway || _agent.IsRetreating();

    public SiegeSide Side
    {
        get
        {
            var team = _agent.Team;
            if (team == null) return SiegeSide.None;

            switch (team.Side)
            {
                case BattleSideEnum.Attacker:
                    return SiegeSide.Attacker;
                case BattleSideEnum.Defender:
                    return SiegeSide.Defender;
                default:
                    return SiegeSide.None;
            }
        }
    }

    public SiegePoint Position
    {
        get
        {
            var at = _agent.Position;
            return new SiegePoint(at.x, at.y, at.z);
        }
    }

    public int NavigationFaceId => _agent.GetCurrentNavigationFaceId();

    public bool IsInLadderQueue => _agent.IsInLadderQueue;

    public SiegeFormationState Formation
    {
        get
        {
            var formation = _agent.Formation;
            if (formation == null) return default;

            // Read inside a mission only: MovementOrder's static constructor reads the mission clock.
            ref readonly var order = ref formation.GetReadonlyMovementOrderReference();
            return new SiegeFormationState(!formation.IsAIControlled, OrderOf(order.OrderEnum));
        }
    }

    public bool HasScriptedPosition => (_agent.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0;

    public bool IsAttackingEntity => (_agent.GetScriptedCombatFlags() & Agent.AISpecialCombatModeFlags.AttackEntity) != 0;

    public void ExcludeFace(int faceGroupId) => _agent.SetAgentExcludeStateForFaceGroupId(faceGroupId, true);

    public bool PathExists(SiegePoint target) =>
        _scene.DoesPathExistBetweenPositions(_agent.GetWorldPosition(), WorldPositionAt(target));

    public void Strike(SiegePoint point, float faceX, float faceY, object gate)
    {
        if (gate is not CastleGate castleGate)
            throw new ArgumentException("A strike needs the gate handle the mission adapter made.", nameof(gate));

        ScriptPosition(point, faceX, faceY);
        _agent.SetScriptedTargetEntity(castleGate.GameEntity, Agent.AISpecialCombatModeFlags.None, ignoreIfAlreadyAttacking: true);
    }

    public void Hold(SiegePoint point, float faceX, float faceY) => ScriptPosition(point, faceX, faceY);

    public void ClearCombatTarget() => _agent.DisableScriptedCombatMovement();

    public void Release()
    {
        _agent.DisableScriptedMovement();
        _agent.DisableScriptedCombatMovement();
    }

    // The position has no valid height, as vanilla builds the wait point of a gate (MovementOrder.ComputeAttackEntityWaitPosition):
    // the engine finds the ground under it. The consider-rotation flag keeps the direction; native or's 5 into the stored flags, so
    // they are not exactly "go to position" either way.
    private void ScriptPosition(SiegePoint point, float faceX, float faceY)
    {
        var position = WorldPositionAt(point);
        _agent.SetScriptedPositionAndDirection(ref position, new Vec2(faceX, faceY).RotationInRadians, false,
            Agent.AIScriptedFrameFlags.ConsiderRotation);
    }

    private WorldPosition WorldPositionAt(SiegePoint point) =>
        new WorldPosition(_scene, UIntPtr.Zero, new Vec3(point.X, point.Y, point.Z), false);

    private static SiegeFormationOrder OrderOf(MovementOrder.MovementOrderEnum order)
    {
        switch (order)
        {
            case MovementOrder.MovementOrderEnum.Charge:
                return SiegeFormationOrder.Charge;
            case MovementOrder.MovementOrderEnum.ChargeToTarget:
                return SiegeFormationOrder.ChargeToTarget;
            case MovementOrder.MovementOrderEnum.Retreat:
                return SiegeFormationOrder.Retreat;
            default:
                return SiegeFormationOrder.Other;
        }
    }
}
