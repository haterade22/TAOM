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
/// TWO mechanisms, and the second is the one that makes the archer usable. Position: the seat holds the archer with
/// the engine's position lock (Agent.SetTargetPosition and SetTargetZ every tick) and attaches it to the platform
/// (Agent.SetForceAttachedEntity), which is what vanilla ClimbingMachine sets on a man on a moving ship's net (#1482).
/// The archer's ranged behaviour walks it off the seat toward a firing position it can never reach, and neither a
/// scripted position at the seat nor a correction by teleport stopped that: a teleport is a full native movement
/// reset, and every reload restart measured on 2026-11-19 had one inside it. One teleport remains, for an archer more
/// than <see cref="HowdahSeatMotion.SeatRescueMetres"/> from its seat. Intent: the archer keeps its formation
/// throughout, because a null formation pins MissileRangeAdjusted at 0, and its AI behaviour curves are rewritten
/// because detaching or moving in formation crushes the ranged ones (<see cref="HowdahCrewBehaviourCurves"/>).
///
/// On release, the lock and the attachment are cleared, the archer is dropped to the elephant's ground position, and
/// its curves and detachability are restored. The seat MUST be empty before anything deactivates it: see
/// <see cref="OnMissionEnded"/> and <see cref="TaomHowdahMachine.OnMissionEnded"/>.
/// </summary>
// PUBLIC, not internal: the Modding Kit resolves script components by type and cannot see an internal
// one, so the editor logs "Could not find object class" and may DROP the script when the prefab is saved.
// The engine finds it either way at runtime, which is why this went unnoticed until the Kit was opened.
public class TaomHowdahStandingPoint : StandingPoint
{
    public Agent elephantAgent;
    public Agent elephantRider;

    /// <summary>Set by TaomHowdahMachine.PropagateRefsToSeats, e.g. "[Howdah#2] seat0" (#627).</summary>
    public string LogTag = "[Howdah]";

    /// <summary>
    /// The seat's numbers for the status line (#627, #1482). A healthy archer reaches progress 1.00 with restarts
    /// near its shot count; the failure is a reload pinned at 0.85 with restarts climbing about once a second. The
    /// probe counts a restart against a rescue only when one fell inside it. Progress is sampled only while the
    /// diagnostics toggle is on.
    /// </summary>
    internal HowdahSeatProbe Probe { get; } = new HowdahSeatProbe();

    private IModLogger _logger;
    private IHowdahDiagnosticsSettingsProvider _diagnostics;
    private bool _firstTickLogged;

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
        // member of its formation and the position lock in OnTick is what holds it on the seat.
        ApplyCrewCombatStance(userAgent);
        userAgent.SetWatchState(Agent.WatchState.Alarmed);
        // Per-occupancy, not per-seat: the status line prints these as this rider's.
        Probe.Reset();
        _behaviourClock = new HowdahSampleClock(HowdahCrewBehaviourCurves.ReassertSeconds);
        // The attachment is what carries the archer WITH the platform. Measured on the mumakil (#1482, 2026-11-19,
        // up to 5 m/s): attached seats stayed within 0.11 m with no rescue, while seats with the lock alone lagged
        // up to 3 m and were rescued. It is the vanilla attach, ClimbingMachine.OnUseAction on a moving ship's net.
        // The root, never this seat's own entity: the root is the one TaomHowdahMachine re-frames. Native keeps a
        // raw entity pointer on the agent, so ReleaseAgent clears it.
        userAgent.SetForceAttachedEntity(GameEntity.Root);
    }

    public override void OnUseStopped(Agent userAgent, bool isSuccessful, int preferenceIndex)
    {
        // Fires if the engine removes the agent from this seat outside our ReleaseAgent path.
        _logger?.LogWarning(
            $"{LogTag} OnUseStopped (UNEXPECTED): agent={userAgent?.Name} isSuccessful={isSuccessful} " +
            $"prefIndex={preferenceIndex} rescues={Probe.Rescues}");
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
                $"missionEnded={missionEnded} after {Probe.Rescues} rescues");
            ReleaseAgent();
            return;
        }

        // Only AFTER the release check (#627, data-flow review F-7): this is the only path that empties a seat whose
        // archer died, and a seat left holding a dead agent is the precondition for the mission-end hang. It must not
        // sit behind a guard on a reference the machine happens to clear first.
        // elephantAgent is propagated from TaomHowdahMachine.PropagateRefsToSeats on its first tick.
        if (elephantAgent == null) return;

        Vec3 seatPosition3 = GameEntity.GlobalPosition;
        // Every consumer below hands this straight to native. It comes from the elephant's own position through
        // GameEntity.SetFrame, so nothing on the path validates it.
        if (!HowdahSeatMotion.IsPlaceable(seatPosition3.x, seatPosition3.y, seatPosition3.z)) return;
        Vec3 riderPosition = MovingAgent.Position;
        float seenSquared = (seatPosition3 - riderPosition).LengthSquared;

        // A position LOCK holds the archer, never a correction (#1482, measured 2026-11-19). The archers WALK off
        // their seats by themselves, elephant standing or moving: this seat's archer read 0.95 m/s on its own legs
        // with the elephant still, the 0.15 m correction teleported it back about 16 times a second, every reload
        // restart had a teleport inside it, and it loosed nothing. The scripted position this replaces was meant to
        // stop that walk (#627) and did not. On the mumakil the one locked seat stayed within 0.03 m with no
        // teleport and shot at the rate of the one archer that never walked. The lock is the engine's own hold for
        // a standing point's user, and what ClimbingMachine sets each tick on a ship's net: a target the native
        // movement goes to. Position only, so the archer still turns to aim, and never through Agent.UseGameObject,
        // which would set NoAttack and silence the bow (Agent.cs:4210). SetTargetZ sets the height target and puts
        // the native movement in the gravity-excluded mode; the howdah has no navmesh under it.
        MovingAgent.SetTargetPosition(seatPosition3.AsVec2);
        MovingAgent.SetTargetZ(seatPosition3.z);

        // The safety net: an archer truly off its deck is placed back once, and counted.
        if (HowdahSeatMotion.ShouldCorrect(seenSquared, HowdahSeatMotion.SeatRescueMetres))
        {
            MovingAgent.TeleportToPosition(seatPosition3);
            Probe.NoteRescue();
        }

        // Watch the upper-body action across frames: a draw that never finishes looks the same in a 5 s snapshot
        // whether it is restarting or stalling, and these two counters tell those apart. Behind the same toggle as
        // the log that prints them, so an archer costs nothing extra once diagnostics are off.
        if (_diagnostics?.IsEnabled == true)
            Probe.NoteProgress(MovingAgent.GetCurrentActionProgress(1));

        // Re-apply the crew's behaviour curves on a slow cadence. The mechanism that reaches THIS archer is
        // DefaultMove: a formation re-applying a Move order stamps it on every unit, and its Ranged row is
        // (0.02, 7, 0.04, 20, 0.03), about a hundredth of Melee, exactly like the DefaultDetached set the crew would
        // get if they were detached. An attached archer needs the override reasserted just as much as a detached one.
        if (_behaviourClock.Tick(dt)) ApplyCrewCombatStance(MovingAgent);

        // NO SetActionChannel here (2026-06-10). Forcing act_howdah_stand_bow each tick pinned the archer's
        // upper body in a permanent draw stance — the engine's combat AI could never take channel 0 to run the
        // actual aim→release→reload cycle, so the archer "drew constantly" and never fired. The elephant.md
        // bug-4 history already proved this: let the combat AI drive the bow animation, don't override it. The
        // archer is held in place by the position lock above; animation is the combat AI's job.

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

        // No periodic line here (#627, delta review): a line every 120 frames was 400 to 580 lines a minute per
        // elephant at 200 to 290 fps, each a durable flush. The machine's 5 s status line carries the crew instead
        // (HowdahDiagnosticsReporter), behind the diagnostics toggle.
    }

    /// <summary>
    /// From the machine, right after it re-framed itself: how far this archer is from its seat in the platform's own
    /// frame. Measured here and not in the seat's tick, so the number does not depend on which of the two ticked
    /// first (#1482).
    /// </summary>
    internal void AfterPlatformMoved(in MatrixFrame root)
    {
        Agent archer = MovingAgent;
        if (archer == null || !archer.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(archer)) return;
        Vec3 position = archer.Position;
        Vec3 lag = root.TransformToLocal(in position) - root.TransformToLocal(GameEntity.GlobalPosition);
        Probe.NoteLag(lag.y, lag.Length);
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
        _logger?.LogInfo($"{LogTag} OnEndMission fired — agent={MovingAgent?.Name ?? "null"} rescues={Probe.Rescues}");
        ReleaseAgent();
        base.OnEndMission();
    }

    internal void ForceRelease() => ReleaseAgent();

    private void ReleaseAgent()
    {
        if (MovingAgent == null) return;
        var agent = MovingAgent;
        bool ownsSlot = AgentSlotIdentity.IsCurrentOccupant(agent);
        // The native attach is undone for a DEAD archer too, while its handle still owns its slot: it is a raw
        // entity pointer on the agent, and vanilla clears it at death (Agent.OnRemove stops the object use, which
        // reaches ResetGravityExclusionAndEntityAttachmentOnStopUsageComponent.OnUseStopped). This seat is not the
        // agent's used object, so nothing else would. A recycled handle is never touched: it would clear a
        // stranger's.
        if (ownsSlot)
        {
            agent.SetForceAttachedEntity(WeakGameEntity.Invalid);
            // ClearTargetFrame drops the position lock; excluded-from-gravity false is the native inverse of
            // SetTargetZ (set_target_z puts the movement in the gravity-excluded mode).
            agent.ClearTargetFrame();
            agent.SetExcludedFromGravity(exclude: false, applyAverageGlobalVelocity: false);
        }
        if (ownsSlot && agent.IsActive())
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

            RestoreOrdinaryCombatStance(agent);

            // Vanilla's own re-attach, which clears the agent out of its detachment as well as out of the
            // formation's detached list (Formation.AttachUnit alone does not). A no-op on the shipped path, where
            // the archer was never detached; it exists so the release stays correct if that ever changes.
            agent.TryAttachToFormation();
        }
        _logger?.LogInfo($"{LogTag} Released agent {agent.Name} from seat after {Probe.Rescues} rescues");
        CrewSeatChecks.Report(CrewSeatChecks.HowdahHeldId, LogTag, Probe.Rescues);
        RemoveMovingAgent(agent);
    }
}
