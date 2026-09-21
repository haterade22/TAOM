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

    private IModLogger _logger;
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
            $"carriedV={HowdahDiagnostics.Format((real - legs).Length, 2)}");
    }
}
