using System;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Elephant;

/// <summary>
/// Seats one archer on the elephant's back and keeps it there.
///
/// TWO mechanisms, and the second is the one that makes the archer usable. Position: the seat teleports the archer to
/// its frame, but ONLY once it has drifted past <see cref="ElephantConfig.HowdahSeatDeadbandMetres"/>. Correcting the
/// 0.10 m settle every frame reads to the engine as 30 m/s of real movement and no bow draw ever completes
/// (<see cref="HowdahSeatMotion"/> carries the measurements). Intent: the seat gives the archer a scripted position at
/// the seat itself, which is what stops its ranged behaviour walking it toward a firing position it can never reach.
/// The archer keeps its formation throughout, because a null formation pins MissileRangeAdjusted at 0, and its AI
/// behaviour curves are rewritten because detaching or moving in formation crushes the ranged ones
/// (<see cref="HowdahCrewBehaviourCurves"/>).
///
/// On release, the archer is dropped to the elephant's ground position, its curves and detachability restored, and its
/// scripted movement cleared. The seat MUST be empty before anything deactivates it: see
/// <see cref="OnMissionEnded"/> and <see cref="TaomHowdahMachine.OnMissionEnded"/>.
/// </summary>
internal class TaomHowdahStandingPoint : StandingPoint
{
    public Agent elephantAgent;
    public Agent elephantRider;

    /// <summary>Set by TaomHowdahMachine.PropagateRefsToSeats, e.g. "[Howdah#2] seat0" (#627).</summary>
    public string LogTag = "[Howdah]";

    /// <summary>
    /// Evidence that an archer's draw completes, read out by the diagnostics reporter as prog= and restarts= (#627).
    /// A healthy archer reaches 1.00 with restarts near its shot count; the failure these were built to catch was a
    /// draw pinned at 0.85 with restarts climbing about once a second, which is what nine rounds of not shooting
    /// looked like. Sampled only while the diagnostics toggle is on, and due for deletion once the owed smoke of the
    /// current code is in.
    /// </summary>
    internal int ActionRestarts;
    internal float MaxActionProgress;

    private IModLogger _logger;
    private IHowdahDiagnosticsSettingsProvider _diagnostics;
    private bool _firstTickLogged;
    private int _teleportCount;
    private float _lastActionProgress;

    // The feature's own tested clock rather than a second accumulator (#627, design review P1). A hand-rolled
    // `_timer += dt` latches on a single non-finite dt: NaN >= period is false forever, so the reassert below would
    // stop for the rest of the battle and the archers would quietly go back to not shooting the next time their
    // formation re-stamped its behaviour set. HowdahSampleClock.Tick ignores such a dt.
    private HowdahSampleClock _behaviourClock;

    // A bow-only archer the engine has marked "detached" will not use its bow, and cannot be ordered to fire by the
    // player either: see HowdahCrewBehaviourCurves.
    private static void ApplyCrewCombatStance(Agent agent)
    {
        HumanAIComponent ai = agent?.HumanAIComponent;
        if (ai == null) return;
        ai.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.Ranged,
            HowdahCrewBehaviourCurves.RangedY1, HowdahCrewBehaviourCurves.RangedX2, HowdahCrewBehaviourCurves.RangedY2,
            HowdahCrewBehaviourCurves.RangedX3, HowdahCrewBehaviourCurves.RangedY3);
        ai.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.Melee,
            HowdahCrewBehaviourCurves.MeleeY1, HowdahCrewBehaviourCurves.MeleeX2, HowdahCrewBehaviourCurves.MeleeY2,
            HowdahCrewBehaviourCurves.MeleeX3, HowdahCrewBehaviourCurves.MeleeY3);
        ai.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.GoToPos,
            HowdahCrewBehaviourCurves.GoToPosY1, HowdahCrewBehaviourCurves.GoToPosX2, HowdahCrewBehaviourCurves.GoToPosY2,
            HowdahCrewBehaviourCurves.GoToPosX3, HowdahCrewBehaviourCurves.GoToPosY3);
        // Fire at will, always (Mike, 2026-09-19: "the archers should also be fire at will since we won't really be
        // able to control them"). Note what this now costs, since the crew are ordinary members of the player's
        // formation again: a player Hold Your Fire reaches them and this reasserts over it within half a second, with
        // no tell but the fire= field in the log. That is the intended behaviour, not an oversight; read the
        // formation's own FiringOrder here instead if the crew should ever obey it (#627, data-flow review F-5).
        agent.SetFiringOrder(FiringOrder.RangedWeaponUsageOrderEnum.FireAtWill);
    }

    /// <summary>
    /// The exact inverse of <see cref="ApplyCrewCombatStance"/>, and it must stay that way: anything added there
    /// needs undoing here (#627, design review P4). Without it a released archer keeps GoToPos and Melee flat zero,
    /// because OverrideBehaviorParams latches the value set to Overriden, and so rejoins its formation able to shoot
    /// but not to advance or defend itself, until that formation happens to re-apply a movement order.
    /// </summary>
    private static void RestoreOrdinaryCombatStance(Agent agent)
    {
        agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Default);
        // Seated archers must not be detachable; a released one is an ordinary unit again.
        agent.SetDetachableFromFormation(true);
    }

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
        _diagnostics = IoC.Resolve<IHowdahDiagnosticsSettingsProvider>();
        _logger?.LogInfo($"{LogTag} Seat OnInit — entity={GameEntity.Name}");
    }

    public override TickRequirement GetTickRequirement()
        => TickRequirement.Tick | base.GetTickRequirement();

    public override bool IsUsableByAgent(Agent userAgent)
        => MovingAgent == null && base.IsUsableByAgent(userAgent);

    public override void OnUse(Agent userAgent, sbyte agentBoneIndex)
    {
        if (MovingAgent != null) return;
        LockUserPositions = true;
        LockUserFrames = true;
        AddMovingAgent(userAgent);
        // NOT detachable while seated (#627, data-flow review F-6). The old call passed `true`, which is the engine
        // default (Agent.cs `_isDetachableFromFormation`), so it did nothing, and it asked for the opposite of what
        // the seat needs: DetachmentManager.TickAgent skips an agent only when it is NOT detachable, and otherwise
        // scores this archer against every detachment its formation holds, including the TaskForceDetachment a
        // formation makes for itself in an ordinary field battle. An archer the seat teleports must not be handed to
        // a machine. Setting false also clears any detachment scores it already accrued.
        userAgent.SetDetachableFromFormation(false);
        // KEEP the formation (#627, 2026-09-19). Nulling it was the upstream pack's way of killing the "walk to your
        // ground slot" order, and it also killed the shooting: Agent.MissileRangeAdjusted (Agent.cs:764) resolves
        // through GetMissileRangeWithHeightDifference (Agent.cs:5444-5450), which returns 0f whenever Formation is
        // null, so a formation-less archer reads as able to reach nothing. Measured 2026-09-19: 14 crewed elephants,
        // 112 status samples, act_none on every seat, not one arrow in a whole battle. The archer stays an ordinary
        // member of its formation and the scripted position in OnTick is what holds it on the seat.
        ApplyCrewCombatStance(userAgent);
        userAgent.SetWatchState(Agent.WatchState.Alarmed);
        // Per-occupancy, not per-seat: the status line prints these as this rider's.
        ActionRestarts = 0;
        MaxActionProgress = 0f;
        _lastActionProgress = 0f;
        _teleportCount = 0;
        _behaviourClock = new HowdahSampleClock(HowdahCrewBehaviourCurves.ReassertSeconds);
    }

    public override void OnUseStopped(Agent userAgent, bool isSuccessful, int preferenceIndex)
    {
        // Fires if the engine removes the agent from this seat outside our ReleaseAgent path.
        _logger?.LogWarning(
            $"{LogTag} OnUseStopped (UNEXPECTED): agent={userAgent?.Name} isSuccessful={isSuccessful} " +
            $"prefIndex={preferenceIndex} ticks={_teleportCount}");
        base.OnUseStopped(userAgent, isSuccessful, preferenceIndex);
    }

    protected override void OnTick(float dt)
    {
        base.OnTick(dt);

        if (IsDisabled) return;
        if (MovingAgent == null) return;

        bool missionEnded = Mission.Current != null && Mission.Current.MissionEnded;
        // The seated rider is a retained handle too: through a recycled slot IsActive answers for a
        // stranger, so the seat would teleport whoever inherited the index (#595, Codex review 109).
        if (!MovingAgent.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(MovingAgent) || missionEnded)
        {
            _logger?.LogInfo(
                $"{LogTag} Releasing {MovingAgent.Name}: isActive={MovingAgent.IsActive()} " +
                $"missionEnded={missionEnded} after {_teleportCount} ticks");
            ReleaseAgent();
            return;
        }

        // Only AFTER the release check (#627, data-flow review F-7): this is the only path that empties a seat whose
        // archer died, and a seat left holding a dead agent is the precondition for the mission-end hang. It must not
        // sit behind a guard on a reference the machine happens to clear first.
        // elephantAgent is propagated from TaomHowdahMachine.PropagateRefsToSeats on its first tick.
        if (elephantAgent == null) return;

        // Directly place agent at the seat entity's current world position every frame.
        // TeleportToPosition calls MBAPI.IMBAgent.SetPosition with the Vec3 as-is (no navmesh
        // Z-snap), so the elevated seat Z (~3.2m above elephant) is preserved.
        // This counteracts the navmesh gravity that snapped agents to terrain Z on spawn.
        // ADOD_Beasts uses the equivalent pattern via a NativeHook SetPosition each tick.
        // Only when it has actually drifted: see ElephantConfig.HowdahSeatDeadbandMetres. A resting archer sits about
        // 0.10 m off its frame, and correcting that every frame is indistinguishable from being thrown around.
        Vec3 seatPosition3 = GameEntity.GlobalPosition;
        // Both consumers below hand this straight to native (TeleportToPosition, SetScriptedPosition). It comes from
        // the elephant's own position through GameEntity.SetFrame, so nothing on the path validates it.
        if (!HowdahSeatMotion.IsPlaceable(seatPosition3.x, seatPosition3.y, seatPosition3.z)) return;
        Vec3 riderPosition = MovingAgent.Position;
        if (HowdahSeatMotion.ShouldCorrect((seatPosition3 - riderPosition).LengthSquared,
                ElephantConfig.HowdahSeatDeadbandMetres))
        {
            MovingAgent.TeleportToPosition(seatPosition3);
        }

        // Tell the archer it has ARRIVED (#627, 2026-09-20). Measured with the elephant standing still: each archer's
        // own locomotion read 3.18 m/s and its real movement 31 m/s, because its ranged behaviour walks toward a
        // firing position it can never reach while the teleport above yanks it back about 10 cm every frame. Nothing
        // finishes a bow draw at that speed: the draw stuck at 85 percent and re-nocked about once a second at every
        // range, moving or stopped. Zeroing MovementInputVector did nothing, because the agent's own AI tick runs on
        // the asynchronous thread and writes it again after ours. A scripted position is the engine's own lever, the
        // same one Agent.UseGameObject sets for a standing point: the destination is where the archer already is, so
        // it stops walking. DoNotRun and no NoAttack, because NoAttack is exactly what would silence the bow
        // (Agent.cs:4210 sets it when a standing point locks its user's frame, which is why this seat does not use
        // that path). It follows the seat every tick because the elephant moves.
        // hasValidZ MATTERS (#627, engine review E1). The two-argument WorldPosition constructor passes
        // hasValidZ: false, and SetScriptedPosition ends with: if (Mission.IsTeleportingAgents && the XY differs)
        // TeleportToPosition(position.GetGroundVec3()). GetGroundVec3 on an invalid-Z position resolves Z natively to
        // the GROUND, which would drop the archer off the howdah and into the elephant's own capsule for the whole
        // deployment phase, the one window where the engine sets that flag. Declaring the Z valid keeps our 3.2 m and
        // skips a native Z resolution every frame (WorldPosition.ValidateZ only calls out when State < requested).
        var seatPosition = new WorldPosition(Mission.Current?.Scene, UIntPtr.Zero, seatPosition3, hasValidZ: true);
        MovingAgent.SetScriptedPosition(ref seatPosition, addHumanLikeDelay: false, Agent.AIScriptedFrameFlags.DoNotRun);

        // Watch the upper-body action across frames: a draw that never finishes looks the same in a 5 s snapshot
        // whether it is restarting or stalling, and these two counters tell those apart. Behind the same toggle as
        // the log that prints them, so an archer costs nothing extra once diagnostics are off.
        if (_diagnostics?.IsEnabled == true)
        {
            float progress = MovingAgent.GetCurrentActionProgress(1);
            if (progress < _lastActionProgress - 0.01f) ActionRestarts++;
            if (progress > MaxActionProgress) MaxActionProgress = progress;
            _lastActionProgress = progress;
        }

        // Re-apply the crew's behaviour curves on a slow cadence. The mechanism that reaches THIS archer is
        // DefaultMove: a formation re-applying a Move order stamps it on every unit, and its Ranged row is
        // (0.02, 7, 0.04, 20, 0.03), about a hundredth of Melee, exactly like the DefaultDetached set the crew would
        // get if they were detached. An attached archer needs the override reasserted just as much as a detached one.
        if (_behaviourClock.Tick(dt)) ApplyCrewCombatStance(MovingAgent);

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
                $"{LogTag} OnTick FIRST FIRE — agent={MovingAgent.Name} " +
                $"seatPos={GameEntity.GlobalPosition} agentPos={MovingAgent.Position} " +
                $"elephantFeet={(AgentSlotIdentity.IsCurrentOccupant(elephantAgent) ? elephantAgent.Position.z : float.NaN):F1} " +
                $"isActive={MovingAgent.IsActive()} hasRanged={MovingAgent.HasRangedWeapon(false)} " +
                $"action={MovingAgent.GetCurrentAction(0).GetName()}");
        }

        // No periodic line here (#627, delta review): _teleportCount counts FRAMES, so at 200 to 290 fps four seats
        // wrote 400 to 580 lines a minute per elephant, each a durable flush. The machine's 5 s status line carries the
        // crew instead (HowdahDiagnosticsReporter), behind the diagnostics toggle.
    }

    /// <summary>
    /// The seat's half of the anti-hang release, and the BACKSTOP rather than the load-bearing half (#627).
    /// Nothing but the machine sets IsDeactivated on a seat, and the machine's override empties every seat before
    /// base.OnMissionEnded deactivates them, so that one is sufficient in either call order and this one alone is
    /// not. This covers a seat that outlives its machine. If one of the two is ever deleted, delete this one.
    /// Both are idempotent: ReleaseAgent returns immediately once MovingAgent is null.
    /// </summary>
    public override void OnMissionEnded()
    {
        ReleaseAgent();
        base.OnMissionEnded();
    }

    public override void OnEndMission()
    {
        _logger?.LogInfo($"{LogTag} OnEndMission fired — agent={MovingAgent?.Name ?? "null"} total ticks={_teleportCount}");
        ReleaseAgent();
        base.OnEndMission();
    }

    internal void ForceRelease() => ReleaseAgent();

    private void ReleaseAgent()
    {
        if (MovingAgent == null) return;
        var agent = MovingAgent;
        if (agent.IsActive() && AgentSlotIdentity.IsCurrentOccupant(agent))
        {
            // Drop to elephant feet level (terrain Z) so the exit sequencer can pathfind normally.
            // TeleportToPosition calls MBAPI.SetPosition with no Z-snap; elephantAgent.Position
            // is already at terrain Z, so this correctly brings the agent down from the howdah.
            // A dead elephant's handle reads its recycled engine slot, so its Position could be any
            // agent's (#592, #595). Only a handle that still owns its index may place the rider.
            Agent elephant = elephantAgent;
            var groundPos = elephant != null && AgentSlotIdentity.IsCurrentOccupant(elephant)
                ? elephant.Position
                : agent.Position;
            agent.TeleportToPosition(groundPos);

            // Drop the scripted position with the seat, or the archer keeps walking at where the howdah used to be.
            agent.DisableScriptedMovement();

            RestoreOrdinaryCombatStance(agent);

            // Vanilla's own re-attach, which clears the agent out of its detachment as well as out of the
            // formation's detached list (Formation.AttachUnit alone does not). A no-op on the shipped path, where
            // the archer was never detached; it exists so the release stays correct if that ever changes.
            agent.TryAttachToFormation();
        }
        _logger?.LogInfo($"{LogTag} Released agent {agent.Name} from seat after {_teleportCount} ticks");
        RemoveMovingAgent(agent);
    }
}
