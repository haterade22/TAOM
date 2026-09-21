using System;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Elephant;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Mumakil;

/// <summary>
/// Carries the Mûmakil's crew: an invisible platform entity re-framed onto the beast every tick, holding the crew
/// frames the archers stand on. A clone of <see cref="TaomHowdahMachine"/> per the one-feature-per-creature
/// convention, with one thing the howdah does not need.
///
/// SCALE. The tower is drawn at elephant scale in the FBX and the engine grows the mount to 3.0x from the Horse
/// item's BodyLength=300. The prefab is therefore authored mount-local, 1:1 with the mesh, and this class scales
/// the entity by the mount's own <see cref="Agent.AgentScale"/> every time it re-frames it. Nothing anywhere
/// writes 3.0 down: change BodyLength and the crew follow it. Bake it in and they end up inside the beast.
///
/// The seats must be EMPTY before anything deactivates them. UsableMissionObject.IsDeactivated's setter spins
/// `while (HasAIMovingTo) { MovingAgent.StopUsingGameObject(); }` (UsableMissionObject.cs:129-133), and an agent
/// registered with AddMovingAgent alone can never clear it, so the loop never exits: a dead mission tick at full
/// frame rate with no crash and no log line. That hung two elephant battles on 2026-09-19 and was found from a
/// hang dump. Both <see cref="OnMissionEnded"/> and <see cref="Disable"/> release first.
/// </summary>
public class TaomMumakilPlatform : UsableMachine
{
    /// <summary>The beast this platform rides. Set by MumakilMissionBehavior right after instantiation.</summary>
    public Agent mumakilAgent;

    /// <summary>Per-mission serial for the log, e.g. "[Mumakil#2]". Agent indices recycle, so this is not one.</summary>
    public string LogTag = "[Mumakil]";

    /// <summary>The bone the tower MESH is skinned to (dominant weight, measured in the FBX 2026-09-21:
    /// Spine1_05 60533, Spine_04 55027, Pelvis_03 37533). This platform is framed from the agent ROOT, so the
    /// two only agree while that bone sits at its rest height. The probe below measures the difference.</summary>
    private const string SpineBoneName = "Spine1_05";

    private IModLogger _logger;
    private bool _boneResolved;
    private sbyte _boneIndex = -1;
    private IHowdahDiagnosticsSettingsProvider _diagnostics;
    private readonly HowdahSampleClock _statusClock =
        new HowdahSampleClock(MumakilConfig.PlatformStatusPeriodSeconds, fireOnFirstTick: true);
    private bool _seatsInitialised;

    public override TextObject GetActionTextForStandingPoint(UsableMissionObject usableGameObject)
        => new TextObject("Climb");

    public override TextObject GetDescriptionText(WeakGameEntity gameEntity)
        => new TextObject("Mumakil Platform");

    // 0 = vanilla detachment never assigns anyone here; the crew are placed by MumakilCrewSpawner.
    protected override float GetDetachmentWeightAux(BattleSideEnum side) => 0f;

    protected override void OnInit()
    {
        base.OnInit();
        _logger = IoC.Resolve<IModLogger>();
        _diagnostics = IoC.Resolve<IHowdahDiagnosticsSettingsProvider>();
        PropagateRefsToSeats();
    }

    public override TickRequirement GetTickRequirement()
        => TickRequirement.Tick | base.GetTickRequirement();

    protected override void OnTick(float dt)
    {
        if (!_seatsInitialised) PropagateRefsToSeats();

        if (Mission.Current != null && Mission.Current.MissionEnded)
        {
            ReleaseAllSeats();
            return;
        }

        if (mumakilAgent == null) return;
        // A dead beast's handle keeps reading its recycled engine slot, so the platform would follow whoever
        // inherits the index (#592, #595).
        if (!mumakilAgent.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(mumakilAgent))
        {
            _logger?.LogInfo($"{LogTag} mumakil gone: releasing seats and clearing refs");
            ReleaseAllSeats();
            ClearSeatRefs();
            mumakilAgent = null;
            return;
        }

        RepositionToMount();
        if (_statusClock.Tick(dt) && _diagnostics?.IsEnabled == true) LogStatus();
    }

    /// <summary>Places AND scales the platform onto the beast. Public so the behaviour can call it once before the
    /// crew spawn, while the seats' world positions still need to be real.</summary>
    public void RepositionToMount()
    {
        // Gated HERE and not only in OnTick, because this is public and the crew spawner calls it outside the
        // tick's guards. A dead beast's handle still answers for whoever inherited its engine slot (#592, #595).
        if (mumakilAgent == null || !AgentSlotIdentity.IsCurrentOccupant(mumakilAgent)) return;
        float scale = mumakilAgent.AgentScale;
        if (!HowdahSeatMotion.IsPlaceable(scale, scale, scale) || scale <= 0f) scale = 1f;

        MatrixFrame next = mumakilAgent.Frame;
        next.origin = mumakilAgent.Position;
        // Orthonormalise BEFORE scaling (#627 phase 2 review, U1). Agent.Frame is a native read
        // (MBAPI.IMBAgent.GetRotationFrame) and whether the basis it returns already carries the agent's scale cannot
        // be settled from managed code. If it does and we simply multiplied, the platform would be scaled twice and
        // the crow's nest would sit at 41 m instead of 13.8. Stripping the basis to unit length first makes the
        // result identical either way, which is worth more than being right about a native detail nothing can test.
        // Mat3.MakeUnit() would strip the scale just as well; Orthonormalize also squares the basis, for nothing.
        next.rotation.Orthonormalize();
        // Position is an engine float heading for the same native write as the scale above, so it gets the same
        // gate: check every float-to-decision path in the method, not only the lines you added
        // (.claude/rules/csharp-architecture.md, "Engine-Float Decision Gates").
        if (!HowdahSeatMotion.IsPlaceable(next.origin.x, next.origin.y, next.origin.z)) return;
        // The prefab is authored mount-local, so the mount's own scale is what maps it onto the beast.
        next.rotation.ApplyScaleLocal(scale);
        GameEntity.SetFrame(ref next);
    }

    private void PropagateRefsToSeats()
    {
        if (mumakilAgent == null) return;
        int i = 0;
        foreach (StandingPoint sp in StandingPoints)
        {
            if (sp is TaomMumakilStandingPoint seat)
            {
                seat.mumakilAgent = mumakilAgent;
                seat.LogTag = $"{LogTag} seat{i}";
            }
            i++;
        }
        _seatsInitialised = true;
    }

    private void ClearSeatRefs()
    {
        foreach (StandingPoint sp in StandingPoints)
            if (sp is TaomMumakilStandingPoint seat)
                seat.mumakilAgent = null;
        _seatsInitialised = false;
    }

    private void ReleaseAllSeats()
    {
        foreach (StandingPoint sp in StandingPoints)
            if (sp is TaomMumakilStandingPoint seat && seat.MovingAgent != null)
                seat.ForceRelease();
    }

    /// <summary>Empties every seat before vanilla deactivates it. See the class remarks: this override is the one
    /// that prevents the hang, because nothing else sets IsDeactivated on a seat.</summary>
    public override void OnMissionEnded()
    {
        ReleaseAllSeats();
        base.OnMissionEnded();
    }

    /// <summary>The second copy of the same loop, closed before anything can reach it. Vanilla's Deactivate() has
    /// the same shape and is NOT virtual, so never route a mumakil platform through a SiegeWeapon-style controller.</summary>
    public override void Disable()
    {
        ReleaseAllSeats();
        base.Disable();
    }

    public override void OnEndMission()
    {
        ReleaseAllSeats();
        mumakilAgent = null;
        base.OnEndMission();
    }

    private void LogStatus()
    {
        int seated = 0, shooting = 0;
        foreach (StandingPoint sp in StandingPoints)
        {
            if (!(sp is TaomMumakilStandingPoint seat) || seat.MovingAgent == null) continue;
            Agent rider = seat.MovingAgent;
            if (!AgentSlotIdentity.IsCurrentOccupant(rider)) continue;
            seated++;
            if (rider.MissileRangeAdjusted > 0f) shooting++;
        }
        // Max drift tells apart "the archers sit still on their seats" from "they are being pushed off and
        // teleported back every frame"; the correction count alone cannot, since it has no distance in it.
        float maxDrift = 0f;
        foreach (StandingPoint sp in StandingPoints)
        {
            if (!(sp is TaomMumakilStandingPoint s2) || s2.MovingAgent == null || !s2.GameEntity.IsValid) continue;
            float d = (s2.GameEntity.GlobalPosition - s2.MovingAgent.Position).Length;
            if (!float.IsNaN(d) && d > maxDrift) maxDrift = d;
        }
        Vec3 real = mumakilAgent.GetAverageRealGlobalVelocity();
        Vec3 legs = mumakilAgent.AverageVelocity;
        // topSeatZ makes the smoke self-proving: the decks sit 9.00, 11.40 and 13.80 m above the beast's feet. Near
        // 3 to 5 means the scale never reached the platform; near 27 to 41 means it was applied twice.
        float feet = mumakilAgent.Position.z;
        float topSeatZ = float.NaN;
        foreach (StandingPoint sp in StandingPoints)
            if (sp is TaomMumakilStandingPoint s && s.GameEntity.IsValid)
            {
                float z = s.GameEntity.GlobalPosition.z - feet;
                if (float.IsNaN(topSeatZ) || z > topSeatZ) topSeatZ = z;
            }
        _logger?.LogInfo(
            $"{LogTag} status scale={HowdahDiagnostics.Format(mumakilAgent.AgentScale, 2)} " +
            // frameScale settles a question managed code cannot: whether the native GetRotationFrame behind
            // Agent.Frame already carries AgentScale. 1.00 means it does not, 3.00 means it does and the
            // Orthonormalize above is the only reason the decks are not at 41 m. Delete once a log has answered it.
            $"frameScale={HowdahDiagnostics.Format(mumakilAgent.Frame.rotation.GetScaleVector().z, 2)} " +
            $"topSeatZ={HowdahDiagnostics.Format(topSeatZ, 2)} " +
            $"seated={seated}/{StandingPoints.Count} withRange={shooting} " +
            $"realV={HowdahDiagnostics.Format(real.Length, 2)} legsV={HowdahDiagnostics.Format(legs.Length, 2)} " +
            $"carriedV={HowdahDiagnostics.Format((real - legs).Length, 2)} " +
            $"maxDrift={HowdahDiagnostics.Format(maxDrift, 2)} {BoneProbe()}");
    }

    /// <summary>
    /// Where the tower's own bone actually is, relative to the beast's feet (#627 phase 2, 2026-09-21).
    ///
    /// The tower the player sees is an AdditionalMesh handed to the agent's SKELETON and skinned to
    /// <see cref="SpineBoneName"/>; this platform is framed from the agent ROOT. In the FBX rest pose that bone
    /// head sits 2.199 authored above the feet and the main deck 0.803 above it, so at 3.0x the deck belongs
    /// 9.01 m up ONLY IF the bone is at 6.60 m in game. Read boneAboveFeet in the log: 6.60 means the decks are
    /// where this platform puts them and any placement error is elsewhere; anything higher is the distance every
    /// archer is standing below its deck, and the fix is to frame the platform from the bone rather than the root.
    ///
    /// Index-based and bounds-checked, never the by-name frame API, which faults on a missing name.
    /// </summary>
    private string BoneProbe()
    {
        try
        {
            var visuals = mumakilAgent?.AgentVisuals;
            Skeleton skel = visuals?.GetSkeleton();
            if (skel == null) return "bone=noskeleton";

            int boneCount = skel.GetBoneCount();
            if (!_boneResolved)
            {
                _boneResolved = true;
                _boneIndex = TaomHowdahMachine.ResolveBoneIndex(skel, SpineBoneName, boneCount);
            }
            if (_boneIndex < 0 || _boneIndex >= boneCount) return $"bone=missing(count={boneCount})";

            MatrixFrame boneLocal = skel.GetBoneEntitialFrameWithIndex(_boneIndex);
            MatrixFrame world = visuals.GetGlobalFrame().TransformToParent(in boneLocal);

            // Into the BEAST's own frame. A world-space delta turns with the animal and answers nothing;
            // agent-local is the same space the prefab's frames are authored in, so the two are comparable.
            MatrixFrame agentFrame = mumakilAgent.Frame;
            agentFrame.origin = mumakilAgent.Position;
            agentFrame.rotation.Orthonormalize();
            Vec3 boneL = agentFrame.TransformToLocal(world.origin);

            // The discriminator. The FBX puts this bone's head at y +1.027, z 2.199 in authoring units,
            // so at 3.0x it belongs at y +3.08, z +6.60 of the beast's own frame. If boneY reads about
            // -3.08 the prefab's axes are the FBX's turned 180 degrees, which is what the crew frames
            // assume. If it reads about +3.08 they are not, and every frame is at the wrong end of the
            // animal: the archers would be told to stand off the front of it, with nothing underneath.
            string seats = "";
            int n = 0;
            foreach (StandingPoint sp in StandingPoints)
            {
                if (!(sp is TaomMumakilStandingPoint s3) || !s3.GameEntity.IsValid) continue;
                Vec3 sl = agentFrame.TransformToLocal(s3.GameEntity.GlobalPosition);
                seats += $" seat{n}L=({HowdahDiagnostics.Format(sl.x, 2)},{HowdahDiagnostics.Format(sl.y, 2)},{HowdahDiagnostics.Format(sl.z, 2)})";
                if (s3.MovingAgent != null)
                {
                    Vec3 al = agentFrame.TransformToLocal(s3.MovingAgent.Position);
                    seats += $"/archer=({HowdahDiagnostics.Format(al.x, 2)},{HowdahDiagnostics.Format(al.y, 2)},{HowdahDiagnostics.Format(al.z, 2)})";
                }
                if (++n >= 3) break;
            }
            return $"boneIdx={_boneIndex} boneL=({HowdahDiagnostics.Format(boneL.x, 2)}," +
                   $"{HowdahDiagnostics.Format(boneL.y, 2)},{HowdahDiagnostics.Format(boneL.z, 2)})" +
                   $" expect(+3.08z6.60){seats}";
        }
        catch (Exception ex)
        {
            return "bone=error:" + ex.GetType().Name;
        }
    }
}
