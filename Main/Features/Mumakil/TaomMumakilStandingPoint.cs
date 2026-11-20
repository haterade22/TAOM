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
/// rules #627 cost nine in-game rounds to find. It does NOT copy their numbers: the rescue band, the capsule spacing
/// and the AI curve rows come from the pure <see cref="HowdahSeatMotion"/> and <see cref="HowdahCrewBehaviourCurves"/>,
/// which are measurements rather than creature behaviour.
///
/// The four rules, each of which silently stops an archer shooting:
/// 1. KEEP the formation. A null one pins Agent.MissileRangeAdjusted at 0 (Agent.cs:764 ->
///    GetMissileRangeWithHeightDifference, Agent.cs:5444-5450) and the archer reads as able to reach nothing.
/// 2. Rewrite the AI behaviour curves and keep rewriting them. A formation applying a Move order stamps
///    DefaultMove, whose Ranged row is about a hundredth of Melee (HumanAIComponent.cs:751-758).
/// 3. Hold the archer with the engine's position lock and attach it to the platform (#1482). Its ranged behaviour
///    walks it off the seat toward a firing position it can never reach, beast standing or moving, and a scripted
///    position at the seat did not stop that. Never NoAttack, which is the flag that silences the bow.
/// 4. Never hold it by teleport. A teleport is a full native movement reset: every reload restart measured on
///    2026-11-19 had one inside it, and correcting the settle every frame reads to the engine as tens of m/s
///    (#627). One teleport remains, for an archer more than <see cref="HowdahSeatMotion.SeatRescueMetres"/> off.
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

    private IModLogger _logger;
    private HowdahSampleClock _behaviourClock;

    // The seat's numbers for the platform's status line. The bow lives on upper-body channel 1: a reload or draw
    // that completes runs its progress to 1.0; one the engine aborts falls back toward 0 and starts again, which is
    // the re-nock loop. The probe counts a restart against a rescue only when one fell inside it (#1482).
    internal HowdahSeatProbe Probe { get; } = new HowdahSeatProbe();
    internal string UpperBodyAction =>
        MovingAgent != null && MovingAgent.IsActive() ? MovingAgent.GetCurrentAction(1).GetName() : "-";

    protected override void OnInit()
    {
        base.OnInit();
        // Cleared so that if anything ever routes this seat through Agent.UseGameObject, it does not set
        // AIScriptedFrameFlags.NoAttack (Agent.cs:4210) and silence the bow.
        LockUserFrames = false;
        LockUserPositions = false;
        _logger = IoC.Resolve<IModLogger>();
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
        // Speed limit 0, once per occupancy. The first battle with a navmesh under the crew they wandered up to
        // 7.06 m for better firing positions. The lock in OnTick now holds the position; both battles that
        // measured the lock (2026-11-19) had this limit on, so whether the lock needs it is not known. Locomotion
        // only, turning and shooting untouched.
        userAgent.SetMaximumSpeedLimit(0f, isMultiplier: false);
        userAgent.SetWatchState(Agent.WatchState.Alarmed);
        _behaviourClock = new HowdahSampleClock(HowdahCrewBehaviourCurves.ReassertSeconds);
        Probe.Reset();
        // The attachment is what carries the archer WITH the platform (#1482, 2026-11-19, one mumakil at up to
        // 5 m/s): three attached seats stayed within 0.11 m of their seats with no rescue, while five seats with the
        // lock alone lagged up to 3 m and were rescued. It is the vanilla attach, ClimbingMachine.OnUseAction on a
        // moving ship's net. The root, never this seat's own entity: the root is the one TaomMumakilPlatform
        // re-frames. Native keeps a raw entity pointer on the agent, so ReleaseAgent clears it.
        userAgent.SetForceAttachedEntity(GameEntity.Root);
    }

    public override void OnUseStopped(Agent userAgent, bool isSuccessful, int preferenceIndex)
    {
        _logger?.LogWarning(
            $"{LogTag} OnUseStopped (UNEXPECTED): agent={userAgent?.Name} isSuccessful={isSuccessful} " +
            $"prefIndex={preferenceIndex} rescues={Probe.Rescues}");
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
                $"missionEnded={missionEnded} after {Probe.Rescues} rescues");
            ReleaseAgent();
            return;
        }

        if (mumakilAgent == null) return;

        Vec3 seatPosition = GameEntity.GlobalPosition;
        // Both consumers below hand this straight to native, and it comes from the mount's own position through
        // GameEntity.SetFrame, which nothing validates.
        if (!HowdahSeatMotion.IsPlaceable(seatPosition.x, seatPosition.y, seatPosition.z)) return;

        float seenSquared = (seatPosition - MovingAgent.Position).LengthSquared;

        // A position LOCK holds the archer, never a correction (#1482, measured 2026-11-19 on three mumakil). The
        // archers WALK off their seats by themselves, beast standing or moving: with the old 0.74 m correction a
        // main-deck archer crept to the band and was teleported back 12 times per 5 s, every reload restart had
        // a teleport inside it, and it stopped shooting. With no correction it walked 3 m off its deck. The one
        // locked seat stayed within 0.03 m with no teleport and shot at the nest archer's rate. The lock is the
        // engine's own hold for a standing point's user, and what ClimbingMachine sets each tick on a ship's net:
        // a target the native movement goes to. Position only, so the archer still turns to aim. SetTargetZ
        // sets the height target and puts the native movement in the gravity-excluded mode. The lock alone lags
        // behind a beast at speed; the attachment set in OnUse is what removes that lag.
        MovingAgent.SetTargetPosition(seatPosition.AsVec2);
        MovingAgent.SetTargetZ(seatPosition.z);

        // The safety net: an archer truly off its deck is placed back once, and counted.
        if (HowdahSeatMotion.ShouldCorrect(seenSquared, HowdahSeatMotion.SeatRescueMetres))
        {
            MovingAgent.TeleportToPosition(seatPosition);
            Probe.NoteRescue();
        }

        if (_behaviourClock != null && _behaviourClock.Tick(dt))
            ApplyCrewCombatStance(MovingAgent);

        Probe.NoteProgress(MovingAgent.GetCurrentActionProgress(1));
    }

    /// <summary>
    /// From the platform, right after it re-framed itself: how far this archer is from its seat in the platform's
    /// own frame. Measured here and not in the seat's tick, so the number does not depend on which of the two
    /// ticked first.
    /// </summary>
    internal void AfterPlatformMoved(in MatrixFrame root)
    {
        Agent archer = MovingAgent;
        if (archer == null || !archer.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(archer)) return;
        Vec3 position = archer.Position;
        Vec3 lag = root.TransformToLocal(in position) - root.TransformToLocal(GameEntity.GlobalPosition);
        Probe.NoteLag(lag.y, lag.Length);
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
        // The seat's native state is undone for a DEAD archer too, while its handle still owns its slot: the attach
        // is a raw entity pointer on the agent, and vanilla clears the same state at death (Agent.OnRemove stops
        // the object use, which reaches ResetGravityExclusionAndEntityAttachmentOnStopUsageComponent.OnUseStopped
        // and StandingPoint.OnUseStopped). This seat is not the agent's used object, so nothing else would. A
        // recycled handle is never touched: it would clear a stranger's.
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
            // Down to the mount's feet so the end-battle sequencer can path. A dead mount's handle reads a recycled
            // slot, so only a handle that still owns its index may place the archer (#592, #595).
            Agent mount = mumakilAgent;
            Vec3 ground = mount != null && AgentSlotIdentity.IsCurrentOccupant(mount) ? mount.Position : agent.Position;
            agent.TeleportToPosition(ground);
            RestoreOrdinaryCombatStance(agent);
            agent.TryAttachToFormation();
        }
        _logger?.LogInfo($"{LogTag} released {agent.Name} after {Probe.Rescues} rescues");
        CrewSeatChecks.Report(CrewSeatChecks.MumakilHeldId, LogTag, Probe.Rescues);
        RemoveMovingAgent(agent);
    }
}
