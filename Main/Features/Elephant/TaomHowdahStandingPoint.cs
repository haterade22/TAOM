using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Elephant;

/// <summary>
/// Seats a single harad archer on the elephant's back and carries them with it every frame.
/// Positioning mechanism: <see cref="Agent.TeleportToPosition"/> is called every OnTick with the
/// seat entity's current world position. TeleportToPosition calls MBAPI.IMBAgent.SetPosition
/// with the Vec3 as-is (no navmesh Z-snap) — the upstream beasts pack uses the same pattern via a NativeHook
/// SetPosition. Since the parent howdah entity tracks the elephant each frame via
/// TaomHowdahMachine.RepositionToElephant, this keeps the archer elevated on the howdah platform.
/// On release, TeleportToPosition drops the agent to elephant ground level for the exit sequencer.
/// </summary>
internal class TaomHowdahStandingPoint : StandingPoint
{
    public Agent elephantAgent;
    public Agent elephantRider;

    private IModLogger _logger;
    private bool _firstTickLogged;
    private int _teleportCount;
    private Formation _previousFormation;

    protected override void OnInit()
    {
        base.OnInit();
        // base.OnInit() sets LockUserFrames = !IsInstantUse = true (StandingPoint default).
        // UseGameObject reads LockUserFrames/LockUserPositions to decide whether to set
        // AIScriptedFrameFlags.NoAttack, which would suppress ranged AI attacks.
        // Clear both here so UseGameObject skips the NoAttack path; OnUse restores them afterward.
        LockUserFrames = false;
        LockUserPositions = false;
        _logger = IoC.Resolve<IModLogger>();
        _logger?.LogInfo($"[Howdah] Seat OnInit — entity={GameEntity.Name}");
    }

    public override TickRequirement GetTickRequirement()
        => TickRequirement.Tick | base.GetTickRequirement();

    public override bool IsUsableByAgent(Agent userAgent)
        => MovingAgent == null && base.IsUsableByAgent(userAgent);

    public override void OnUse(Agent userAgent, sbyte agentBoneIndex)
    {
        if (MovingAgent != null) return;
        _logger?.LogInfo(
            $"[Howdah] OnUse: agent={userAgent?.Name} hasRanged={userAgent?.HasRangedWeapon(false)} " +
            $"seatGlobalPos={GameEntity.GlobalPosition} formation={userAgent?.Formation?.FormationIndex}");
        LockUserPositions = true;
        LockUserFrames = true;
        AddMovingAgent(userAgent);
        userAgent.SetDetachableFromFormation(true);
        // Pull the archer OUT of its formation (upstream-pack parity). Kept in HorseArcher, the formation issues
        // "walk to your ground slot" orders every tick — the archer keeps trying to leave the howdah and is
        // teleported back, so it shuffles in place (visible even in the deployment phase). With no formation
        // it auto-fires at nearby enemies instead, driven by the Alarmed watch state. _previousFormation is
        // restored on release so the end-battle exit sequencer behaves.
        _previousFormation = userAgent.Formation;
        userAgent.Formation = null;
        userAgent.SetWatchState(Agent.WatchState.Alarmed);
        _logger?.LogInfo($"[Howdah] OnUse complete: agent seated (formation cleared, watch=Alarmed)");
    }

    public override void OnUseStopped(Agent userAgent, bool isSuccessful, int preferenceIndex)
    {
        // Fires if the engine removes the agent from this seat outside our ReleaseAgent path.
        _logger?.LogWarning(
            $"[Howdah] OnUseStopped (UNEXPECTED): agent={userAgent?.Name} isSuccessful={isSuccessful} " +
            $"prefIndex={preferenceIndex} ticks={_teleportCount}");
        base.OnUseStopped(userAgent, isSuccessful, preferenceIndex);
    }

    protected override void OnTick(float dt)
    {
        base.OnTick(dt);

        if (IsDisabled) return;
        if (MovingAgent == null) return;
        // elephantAgent is propagated from TaomHowdahMachine.PropagateRefsToSeats on its first tick.
        // Skip this seat tick until the machine ref arrives.
        if (elephantAgent == null) return;

        bool missionEnded = Mission.Current != null && Mission.Current.MissionEnded;
        if (!MovingAgent.IsActive() || missionEnded)
        {
            _logger?.LogInfo(
                $"[Howdah] Releasing {MovingAgent.Name}: isActive={MovingAgent.IsActive()} " +
                $"missionEnded={missionEnded} after {_teleportCount} ticks");
            ReleaseAgent();
            return;
        }

        // Directly place agent at the seat entity's current world position every frame.
        // TeleportToPosition calls MBAPI.IMBAgent.SetPosition with the Vec3 as-is (no navmesh
        // Z-snap), so the elevated seat Z (~3.2m above elephant) is preserved.
        // This counteracts the navmesh gravity that snapped agents to terrain Z on spawn.
        // The upstream beasts pack uses the equivalent pattern via a NativeHook SetPosition each tick.
        MovingAgent.TeleportToPosition(GameEntity.GlobalPosition);

        // NO SetActionChannel here (2026-06-10). Forcing act_howdah_stand_bow each tick pinned the archer's
        // upper body in a permanent draw stance — the engine's combat AI could never take channel 0 to run the
        // actual aim→release→reload cycle, so the archer "drew constantly" and never fired. The elephant.md
        // bug-4 history already proved this: let the combat AI drive the bow animation, don't override it. The
        // archer is held in place by TeleportToPosition above; animation is the combat AI's job.

        _teleportCount++;

        if (!_firstTickLogged)
        {
            _firstTickLogged = true;
            _logger?.LogInfo(
                $"[Howdah] OnTick FIRST FIRE — agent={MovingAgent.Name} " +
                $"seatPos={GameEntity.GlobalPosition} agentPos={MovingAgent.Position} " +
                $"elephantFeet={elephantAgent.Position.z:F1} " +
                $"isActive={MovingAgent.IsActive()} hasRanged={MovingAgent.HasRangedWeapon(false)} " +
                $"action={MovingAgent.GetCurrentAction(0).GetName()}");
        }

        if (_teleportCount % 120 == 1)
        {
            _logger?.LogInfo(
                $"[Howdah] Tick#{_teleportCount} agent={MovingAgent.Name} " +
                $"agentPos={MovingAgent.Position} seatPos={GameEntity.GlobalPosition} " +
                $"action={MovingAgent.GetCurrentAction(0).GetName()}");
        }
    }

    public override void OnEndMission()
    {
        _logger?.LogInfo($"[Howdah] OnEndMission fired — agent={MovingAgent?.Name ?? "null"} total ticks={_teleportCount}");
        ReleaseAgent();
        base.OnEndMission();
    }

    internal void ForceRelease() => ReleaseAgent();

    private void ReleaseAgent()
    {
        if (MovingAgent == null) return;
        var agent = MovingAgent;
        if (agent.IsActive())
        {
            // Drop to elephant feet level (terrain Z) so the exit sequencer can pathfind normally.
            // TeleportToPosition calls MBAPI.SetPosition with no Z-snap; elephantAgent.Position
            // is already at terrain Z, so this correctly brings the agent down from the howdah.
            var groundPos = elephantAgent?.Position ?? agent.Position;
            agent.TeleportToPosition(groundPos);
            // Restore the formation we cleared in OnUse so the end-battle exit sequencer can move the agent.
            if (_previousFormation != null)
                agent.Formation = _previousFormation;
        }
        _logger?.LogInfo($"[Howdah] Released agent {agent.Name} from seat after {_teleportCount} ticks");
        RemoveMovingAgent(agent);
    }
}
