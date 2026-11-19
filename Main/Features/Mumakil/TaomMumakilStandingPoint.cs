using System;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Elephant;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Mumakil;

/// <summary>
/// One crew archer's place on the Mûmakil's war tower. A clone of the howdah's seat
/// (<see cref="TaomHowdahStandingPoint"/>) per the one-feature-per-creature convention, carrying the four engine
/// rules #627 cost nine in-game rounds to find. It does NOT copy their numbers: the deadband, the capsule spacing
/// and the AI curve rows come from the pure <see cref="HowdahSeatMotion"/> and <see cref="HowdahCrewBehaviourCurves"/>,
/// which are measurements rather than creature behaviour.
///
/// The four rules, each of which silently stops an archer shooting:
/// 1. KEEP the formation. A null one pins Agent.MissileRangeAdjusted at 0 (Agent.cs:764 ->
///    GetMissileRangeWithHeightDifference, Agent.cs:5444-5450) and the archer reads as able to reach nothing.
/// 2. Rewrite the AI behaviour curves and keep rewriting them. A formation applying a Move order stamps
///    DefaultMove, whose Ranged row is about a hundredth of Melee (HumanAIComponent.cs:751-758).
/// 3. Give the archer a scripted position at its own seat, or its ranged behaviour walks it toward a firing
///    position it can never reach. DoNotRun, and never NoAttack, which is the flag that silences the bow.
/// 4. Only correct its position once it has actually drifted. Correcting the settle every frame reads to the
///    engine as tens of m/s and no bow draw ever completes.
///
/// The seat must also be EMPTY before anything deactivates it: UsableMissionObject.IsDeactivated's setter spins
/// while (HasAIMovingTo) on a MovingAgent that was never registered through AIMoveToGameObjectEnable, which hung
/// two battles on 2026-09-19. <see cref="OnMissionEnded"/> here is the backstop; the platform's override is the
/// load-bearing half.
/// </summary>
// PUBLIC, not internal: the Modding Kit resolves script components by type and cannot see an internal
// one, so the editor logs "Could not find object class" and may DROP the script when the prefab is saved.
// The engine finds it either way at runtime, which is why this went unnoticed until the Kit was opened.
public class TaomMumakilStandingPoint : StandingPoint
{
    /// <summary>The mount this seat rides, set by <see cref="TaomMumakilPlatform"/> on its first tick.</summary>
    public Agent mumakilAgent;

    /// <summary>Set by the platform, e.g. "[Mumakil#2] seat3" (#627 phase 2).</summary>
    public string LogTag = "[Mumakil]";

    /// <summary>The platform this seat is on, set with <see cref="mumakilAgent"/>. The seat reads the frame the
    /// platform last ticked in, which tells whether it saw this frame's seat position or last frame's (#1482).</summary>
    internal TaomMumakilPlatform Platform;

    /// <summary>TEMPORARY (#1482): how this seat carries its archer in the measuring build.</summary>
    internal CrewCarryArm Arm { get; private set; } = CrewCarryArm.Baseline;

    private IModLogger _logger;
    private HowdahSampleClock _behaviourClock;

    // The seat's numbers for the platform's status line. The bow lives on upper-body channel 1: a reload or draw
    // that completes runs its progress to 1.0; one the engine aborts falls back toward 0 and starts again, which is
    // the re-nock loop. The probe counts a restart against a teleport only when one fell inside it (#1482).
    internal HowdahSeatProbe Probe { get; } = new HowdahSeatProbe();
    internal string UpperBodyAction =>
        MovingAgent != null && MovingAgent.IsActive() ? MovingAgent.GetCurrentAction(1).GetName() : "-";

    // ManagedCarry only: where the archer stands in the platform's own frame.
    private Vec3 _carryOffset;
    private bool _hasCarryOffset;

    protected override void OnInit()
    {
        base.OnInit();
        // Cleared so that if anything ever routes this seat through Agent.UseGameObject, it does not set
        // AIScriptedFrameFlags.NoAttack (Agent.cs:4210) and silence the bow.
        LockUserFrames = false;
        LockUserPositions = false;
        _logger = IoC.Resolve<IModLogger>();
        // The diagnostics setting is the experiment's off switch: off at mission start, every seat is Baseline.
        Arm = IoC.Resolve<IHowdahDiagnosticsSettingsProvider>()?.IsEnabled == true
            ? CrewCarryExperiment.ForMumakilSeat(GameEntity.Name)
            : CrewCarryArm.Baseline;
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
        // NOT detachable while seated: DetachmentManager.TickAgent otherwise scores this archer against every
        // detachment its formation holds, and an archer the seat teleports must not be handed to a machine.
        userAgent.SetDetachableFromFormation(false);
        ApplyCrewCombatStance(userAgent);
        // Pinned in place, ONCE. Until the platform carried a navmesh the crew physically could not walk, so
        // nothing ever had to stop them; the first battle with a navmesh under them they wandered up to 7.06 m
        // for better firing positions. Locomotion only, turning and shooting untouched. This is applied here and
        // NOT on the half-second stance clock: re-applying it every 0.5 s on eight seats in lockstep matched the
        // draw dying at 0.85 once a second on all eight in lockstep, while the teleport count did not (340
        // teleports against 27 restarts). A movement-limit reset mid-draw is the prime suspect for the abort.
        userAgent.SetMaximumSpeedLimit(0f, isMultiplier: false);
        userAgent.SetWatchState(Agent.WatchState.Alarmed);
        _behaviourClock = new HowdahSampleClock(HowdahCrewBehaviourCurves.ReassertSeconds);
        Probe.Reset();
        _hasCarryOffset = false;
        // The vanilla attach, ClimbingMachine.OnUseAction on a moving ship's net. The root, never this seat's own
        // entity: the root is the one TaomMumakilPlatform re-frames.
        if (Arm == CrewCarryArm.ForceAttach) userAgent.SetForceAttachedEntity(GameEntity.Root);
    }

    public override void OnUseStopped(Agent userAgent, bool isSuccessful, int preferenceIndex)
    {
        _logger?.LogWarning(
            $"{LogTag} OnUseStopped (UNEXPECTED): agent={userAgent?.Name} isSuccessful={isSuccessful} " +
            $"prefIndex={preferenceIndex} teleports={Probe.Teleports}");
        base.OnUseStopped(userAgent, isSuccessful, preferenceIndex);
    }

    // A bow-only archer whose formation has just re-stamped its behaviour set will not use its bow, and cannot be
    // ordered to fire by the player either: see HowdahCrewBehaviourCurves for the measured rows.
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
        // Nobody can order these archers from the command menu, so the seat sets fire at will itself.
        agent.SetFiringOrder(FiringOrder.RangedWeaponUsageOrderEnum.FireAtWill);

    }

    /// <summary>The exact inverse of <see cref="ApplyCrewCombatStance"/>: anything added there is undone here, or a
    /// released archer keeps GoToPos and Melee flat zero and cannot advance or defend itself.</summary>
    private static void RestoreOrdinaryCombatStance(Agent agent)
    {
        agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Default);
        agent.SetDetachableFromFormation(true);
        // -1 is the engine's own "no limit" value (FollowAgentBehavior clears it exactly this way). Without
        // this a released archer keeps the seat's pin and stands rooted wherever it was put down.
        agent.SetMaximumSpeedLimit(-1f, isMultiplier: false);
    }

    protected override void OnTick(float dt)
    {
        base.OnTick(dt);

        if (IsDisabled) return;
        if (MovingAgent == null) return;

        bool missionEnded = Mission.Current != null && Mission.Current.MissionEnded;
        // BEFORE the mount-reference guard: this is the only path that empties a seat whose archer died, and a
        // seat left holding a dead agent is the precondition for the mission-end hang.
        if (!MovingAgent.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(MovingAgent) || missionEnded)
        {
            _logger?.LogInfo(
                $"{LogTag} releasing {MovingAgent.Name}: isActive={MovingAgent.IsActive()} " +
                $"missionEnded={missionEnded} after {Probe.Teleports} teleports");
            ReleaseAgent();
            return;
        }

        if (mumakilAgent == null) return;

        Vec3 seatPosition = GameEntity.GlobalPosition;
        // Both consumers below hand this straight to native, and it comes from the mount's own position through
        // GameEntity.SetFrame, which nothing validates.
        if (!HowdahSeatMotion.IsPlaceable(seatPosition.x, seatPosition.y, seatPosition.z)) return;

        float seenSquared = (seatPosition - MovingAgent.Position).LengthSquared;
        // Whether the platform was re-framed before this seat ticked. If not, seatPosition is last frame's, and the
        // band below is tested against a seat one frame of travel behind the beast (#1482).
        // Counted only while the status line is on, so a period's numbers cover that period and nothing older.
        if (Platform != null && Platform.Measuring)
            Probe.NoteSeatTick((float)Math.Sqrt(seenSquared), Platform.LastTickFrameNo == Utilities.EngineFrameNo);

        // Baseline is the shipped rule: a body-width band (0.74 m), unscaled since the 2026-09-21 navmesh. Whether
        // the engine carries an archer on the attached navmesh, and whether these teleports are what stall the
        // reload, is what the #1482 measuring build exists to settle; both were asserted here before and neither
        // was measured. Every other arm takes no correction, only a rescue from far off its deck, so one failed
        // arm keeps producing numbers for the rest of the battle.
        if (Arm == CrewCarryArm.Baseline)
        {
            if (HowdahSeatMotion.ShouldCorrect(seenSquared, HowdahSeatMotion.MinimumFrameSeparation))
            {
                MovingAgent.TeleportToPosition(seatPosition);
                Probe.NoteTeleport();
            }
        }
        else if (HowdahSeatMotion.ShouldCorrect(seenSquared, CrewCarryExperiment.RescueBandMetres))
        {
            MovingAgent.TeleportToPosition(seatPosition);
            Probe.NoteRescue();
            _hasCarryOffset = false;
        }

        if (Arm == CrewCarryArm.DrivenTarget)
        {
            // ClimbingMachine's hold: a target the native movement goes to, never a teleport. Position only, so
            // the archer still turns to aim.
            MovingAgent.SetTargetPosition(seatPosition.AsVec2);
            MovingAgent.SetTargetZ(seatPosition.z);
        }
        else
        {
            // hasValidZ matters: the two-argument WorldPosition constructor leaves Z invalid, and
            // SetScriptedPosition resolves an invalid Z to the GROUND whenever Mission.IsTeleportingAgents is set,
            // which the deployment phase does. From 9 m up that would drop the whole crew off the tower.
            var scripted = new WorldPosition(Mission.Current?.Scene, UIntPtr.Zero, seatPosition, hasValidZ: true);
            MovingAgent.SetScriptedPosition(ref scripted, addHumanLikeDelay: false, Agent.AIScriptedFrameFlags.DoNotRun);
        }

        if (_behaviourClock != null && _behaviourClock.Tick(dt))
            ApplyCrewCombatStance(MovingAgent);

        Probe.NoteProgress(MovingAgent.GetCurrentActionProgress(1));
    }

    /// <summary>
    /// From the platform, right after it re-framed itself: how far this archer is from its seat in the platform's
    /// own frame, and for ManagedCarry the placement at its platform-local offset. Measured here and not in the
    /// seat's tick, so the number does not depend on which of the two ticked first.
    /// </summary>
    internal void AfterPlatformMoved(in MatrixFrame root, bool platformMoved, bool measure)
    {
        if (!measure && Arm != CrewCarryArm.ManagedCarry) return;
        Agent archer = MovingAgent;
        if (archer == null || !archer.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(archer)) return;
        Vec3 position = archer.Position;
        if (!HowdahSeatMotion.IsPlaceable(position.x, position.y, position.z)) return;

        if (measure)
        {
            Vec3 lag = root.TransformToLocal(in position) - root.TransformToLocal(GameEntity.GlobalPosition);
            Probe.NoteLag(lag.y, lag.Length);
        }

        if (Arm != CrewCarryArm.ManagedCarry) return;
        if (!_hasCarryOffset)
        {
            Vec3 first = root.TransformToLocal(in position);
            // A NaN offset would answer Leave on every later tick and could never be adopted away.
            if (!HowdahSeatMotion.IsPlaceable(first.x, first.y, first.z)) return;
            _carryOffset = first;
            _hasCarryOffset = true;
            return;
        }
        Vec3 wanted = root.TransformToParent(in _carryOffset);
        switch (HowdahSeatMotion.DecideCarry((wanted - position).LengthSquared, platformMoved,
                    HowdahSeatMotion.CarryToleranceMetres))
        {
            case CarryAction.Place:
                if (!HowdahSeatMotion.IsPlaceable(wanted.x, wanted.y, wanted.z)) break;
                archer.TeleportToPosition(wanted);
                Probe.NoteTeleport();
                break;
            case CarryAction.AdoptOffset:
                Vec3 adopted = root.TransformToLocal(in position);
                if (HowdahSeatMotion.IsPlaceable(adopted.x, adopted.y, adopted.z)) _carryOffset = adopted;
                break;
        }
    }

    /// <summary>Backstop half of the anti-hang release; <see cref="TaomMumakilPlatform.OnMissionEnded"/> is the
    /// load-bearing half, because nothing but the platform sets IsDeactivated on a seat.</summary>
    public override void OnMissionEnded()
    {
        ReleaseAgent();
        base.OnMissionEnded();
    }

    public override void OnEndMission()
    {
        ReleaseAgent();
        base.OnEndMission();
    }

    internal void ForceRelease() => ReleaseAgent();

    private void ReleaseAgent()
    {
        if (MovingAgent == null) return;
        Agent agent = MovingAgent;
        bool ownsSlot = AgentSlotIdentity.IsCurrentOccupant(agent);
        // The arm's native state is undone for a DEAD archer too, while its handle still owns its slot: the attach
        // is a raw entity pointer on the agent, and vanilla clears the same state at death (Agent.OnRemove stops
        // the object use, which reaches ResetGravityExclusionAndEntityAttachmentOnStopUsageComponent.OnUseStopped
        // and StandingPoint.OnUseStopped). This seat is not the agent's used object, so nothing else would. A
        // recycled handle is never touched: it would clear a stranger's.
        if (ownsSlot)
        {
            if (Arm == CrewCarryArm.ForceAttach) agent.SetForceAttachedEntity(WeakGameEntity.Invalid);
            if (Arm == CrewCarryArm.DrivenTarget)
            {
                // ClearTargetFrame drops the position lock; excluded-from-gravity false is the native inverse of
                // SetTargetZ (set_target_z puts the movement in the gravity-excluded mode).
                agent.ClearTargetFrame();
                agent.SetExcludedFromGravity(exclude: false, applyAverageGlobalVelocity: false);
            }
        }
        if (ownsSlot && agent.IsActive())
        {
            // Down to the mount's feet so the end-battle sequencer can path. A dead mount's handle reads a recycled
            // slot, so only a handle that still owns its index may place the archer (#592, #595).
            Agent mount = mumakilAgent;
            Vec3 ground = mount != null && AgentSlotIdentity.IsCurrentOccupant(mount) ? mount.Position : agent.Position;
            agent.TeleportToPosition(ground);
            agent.DisableScriptedMovement();
            RestoreOrdinaryCombatStance(agent);
            agent.TryAttachToFormation();
        }
        _logger?.LogInfo($"{LogTag} released {agent.Name} after {Probe.Teleports} teleports");
        RemoveMovingAgent(agent);
    }
}
