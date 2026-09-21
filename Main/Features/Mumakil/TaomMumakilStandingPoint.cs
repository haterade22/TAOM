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
internal class TaomMumakilStandingPoint : StandingPoint
{
    /// <summary>The mount this seat rides, set by <see cref="TaomMumakilPlatform"/> on its first tick.</summary>
    public Agent mumakilAgent;

    /// <summary>Set by the platform, e.g. "[Mumakil#2] seat3" (#627 phase 2).</summary>
    public string LogTag = "[Mumakil]";

    private IModLogger _logger;
    private HowdahSampleClock _behaviourClock;
    private int _teleportCount;

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
        userAgent.SetWatchState(Agent.WatchState.Alarmed);
        _behaviourClock = new HowdahSampleClock(HowdahCrewBehaviourCurves.ReassertSeconds);
        _teleportCount = 0;
    }

    public override void OnUseStopped(Agent userAgent, bool isSuccessful, int preferenceIndex)
    {
        _logger?.LogWarning(
            $"{LogTag} OnUseStopped (UNEXPECTED): agent={userAgent?.Name} isSuccessful={isSuccessful} " +
            $"prefIndex={preferenceIndex} ticks={_teleportCount}");
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
                $"missionEnded={missionEnded} after {_teleportCount} ticks");
            ReleaseAgent();
            return;
        }

        if (mumakilAgent == null) return;

        Vec3 seatPosition = GameEntity.GlobalPosition;
        // Both consumers below hand this straight to native, and it comes from the mount's own position through
        // GameEntity.SetFrame, which nothing validates.
        if (!HowdahSeatMotion.IsPlaceable(seatPosition.x, seatPosition.y, seatPosition.z)) return;

        // Derived from the mount, never baked: a bigger beast's deck travels further between frames, and this
        // platform's whole design is that nothing writes the 3.0x down (HowdahSeatMotion.SeatDeadbandFor).
        if (HowdahSeatMotion.ShouldCorrect((seatPosition - MovingAgent.Position).LengthSquared,
                HowdahSeatMotion.SeatDeadbandFor(mumakilAgent.AgentScale)))
        {
            MovingAgent.TeleportToPosition(seatPosition);
            _teleportCount++;
        }

        // hasValidZ matters: the two-argument WorldPosition constructor leaves Z invalid, and SetScriptedPosition
        // resolves an invalid Z to the GROUND whenever Mission.IsTeleportingAgents is set, which the deployment
        // phase does. From 9 m up that would drop the whole crew off the tower.
        var scripted = new WorldPosition(Mission.Current?.Scene, UIntPtr.Zero, seatPosition, hasValidZ: true);
        MovingAgent.SetScriptedPosition(ref scripted, addHumanLikeDelay: false, Agent.AIScriptedFrameFlags.DoNotRun);

        if (_behaviourClock != null && _behaviourClock.Tick(dt))
            ApplyCrewCombatStance(MovingAgent);
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
        if (agent.IsActive() && AgentSlotIdentity.IsCurrentOccupant(agent))
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
        _logger?.LogInfo($"{LogTag} released {agent.Name} after {_teleportCount} corrections");
        RemoveMovingAgent(agent);
    }
}
