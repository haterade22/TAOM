using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Elephant;

/// <summary>
/// Engine-facing half of the howdah diagnostics log (#627), owned by one <see cref="TaomHowdahMachine"/>. On sample
/// frames (the first live tick, then every <see cref="ElephantConfig.HowdahStatusPeriodSeconds"/>) it reads the platform
/// and its elephant and writes the [Howdah#n] layout (once), status and summary lines; the arithmetic is in the tested
/// <see cref="HowdahDiagnostics"/> and <see cref="HowdahRunStats"/>. Every other frame costs one clock tick and one int
/// increment. Gated by the MCM toggle Battle Tactics/Howdah Diagnostics, read only when a sample is due.
///
/// The slide signal: Agent.Velocity is only the locomotion velocity rotated into the world, so it cannot see a shove.
/// The engine's own pair, GetAverageRealGlobalVelocity (how the agent actually moved) against AverageVelocity (what its
/// legs produced), is what War Sails uses to tell an agent carried by something else; their difference is logged as
/// carriedV. Its native averaging window is unmeasured, so both raw numbers are logged too.
/// </summary>
internal sealed class HowdahDiagnosticsReporter
{
    private readonly IModLogger _logger;
    private readonly IHowdahDiagnosticsSettingsProvider _settings;
    private readonly HowdahSampleClock _clock = new HowdahSampleClock(ElephantConfig.HowdahStatusPeriodSeconds, fireOnFirstTick: true);
    private readonly HowdahRunStats _stats = new HowdahRunStats();

    private WeakGameEntity _floor;
    private bool _hasFloor;
    private bool _layoutLogged;
    private bool _sampleDue;
    private Vec3 _platformBefore;
    private Vec3 _anchorBefore;
    private bool _hadAnchor;

    public HowdahDiagnosticsReporter(IModLogger logger, IHowdahDiagnosticsSettingsProvider settings)
    {
        _logger = logger;
        _settings = settings;
    }

    private bool Enabled => _logger != null && _settings?.IsEnabled == true;

    /// <summary>Every live tick, BEFORE the re-frame: advances the clock and, on a sample frame, notes where the platform
    /// is now and where the machine put it last frame (drift = the engine or something else moved our entity).</summary>
    public void BeforeReposition(float dt, WeakGameEntity platform, Vec3 lastAnchor, bool hasAnchor)
    {
        _stats.CountTick();
        _sampleDue = _clock.Tick(dt) && Enabled;
        if (!_sampleDue) return;
        _platformBefore = platform.GlobalPosition;
        _anchorBefore = lastAnchor;
        _hadAnchor = hasAnchor;
    }

    /// <summary>Every live tick, AFTER the re-frame: on a sample frame writes the layout (first time only) and a status line.</summary>
    public void AfterReposition(string tag, WeakGameEntity platform, Agent elephant, IReadOnlyList<StandingPoint> seats, string placement)
    {
        if (!_sampleDue) return;
        _sampleDue = false;
        if (!_layoutLogged) LogLayout(tag, platform, elephant, seats);
        LogStatus(tag, elephant, seats, placement);
    }

    /// <summary>Once, when the mission ends. ticks=0 means the machine never ticked with a live elephant.</summary>
    public void LogSummary(string tag, int seatCount)
    {
        if (!Enabled) return;
        _logger.LogInfo(
            $"{tag} summary liveTicks={_stats.Ticks} samples={_stats.Samples} layoutLogged={_layoutLogged} " +
            $"minFloorClearance={HowdahDiagnostics.Format(_stats.MinClearance, 3)} maxDrift={HowdahDiagnostics.Format(_stats.MaxDrift, 3)} " +
            $"maxCarriedV={HowdahDiagnostics.Format(_stats.MaxCarriedSpeed, 2)} invalidValues={_stats.InvalidValues} seats={seatCount}");
    }

    private void LogLayout(string tag, WeakGameEntity platform, Agent elephant, IReadOnlyList<StandingPoint> seats)
    {
        _layoutLogged = true;
        int children = platform.ChildCount, moveable = 0, crew = 0;
        for (int i = 0; i < children; i++)
        {
            WeakGameEntity child = platform.GetChild(i);
            if (!child.IsValid) continue;
            string name = child.Name;
            BodyFlags flags = child.BodyFlag;
            if ((flags & BodyFlags.Moveable) != 0) moveable++;
            if (child.HasTag(ElephantConfig.HowdahCrewTag)) crew++;
            if (name == ElephantConfig.HowdahFloorEntityName)
            {
                _floor = child;
                _hasFloor = true;
            }
            _logger.LogInfo($"{tag} layout child[{i}] name={name} tags={string.Join(",", child.Tags)} flags={flags} pos={Format(child.GlobalPosition)}");
        }

        Monster monster = elephant.Monster;
        float capsuleTop = CapsuleTop(elephant);
        float clearance = FloorClearance(capsuleTop);
        Mission mission = Mission.Current;
        _logger.LogInfo(
            $"{tag} layout summary scene={mission?.SceneName} mode={mission?.Mode} platform={Format(platform.GlobalPosition)} " +
            $"children={children} moveable={moveable} crewFrames={crew} seats={seats.Count} monster={monster?.StringId} " +
            $"capsuleR={HowdahDiagnostics.Format(monster?.BodyCapsuleRadius ?? float.NaN, 2)} capsuleTop={HowdahDiagnostics.Format(capsuleTop, 2)} " +
            $"floorOrigin={(_hasFloor ? Format(_floor.GlobalPosition) : "missing")} floorClearance={HowdahDiagnostics.Format(clearance, 3)}");

        if (seats.Count == 0)
            _logger.LogWarning($"{tag} layout: no seats collected; the crew frames' TaomHowdahStandingPoint scripts did not load");
        if (!_hasFloor)
            _logger.LogWarning($"{tag} layout: no '{ElephantConfig.HowdahFloorEntityName}' child; is an old prefab loaded under this name?");
        else if (HowdahDiagnostics.ClearanceWarrantsWarning(clearance))
            _logger.LogWarning(
                $"{tag} layout: floor origin is {HowdahDiagnostics.Format(clearance, 3)} m from the elephant's capsule top: the floor is " +
                "inside the capsule (a slide risk) or the clearance could not be computed");
    }

    private void LogStatus(string tag, Agent elephant, IReadOnlyList<StandingPoint> seats, string placement)
    {
        Vec3 real = elephant.GetAverageRealGlobalVelocity();
        Vec3 legs = elephant.AverageVelocity;
        float carried = (real - legs).Length;
        float drift = _hadAnchor
            ? HowdahDiagnostics.Distance(_platformBefore.x, _platformBefore.y, _platformBefore.z, _anchorBefore.x, _anchorBefore.y, _anchorBefore.z)
            : float.NaN;
        float clearance = FloorClearance(CapsuleTop(elephant));
        _stats.RecordSample(clearance, drift, carried);

        int seated = 0;
        for (int i = 0; i < seats.Count; i++)
            if (seats[i].MovingAgent != null) seated++;

        _logger.LogInfo(
            $"{tag} status t={HowdahDiagnostics.Format(Mission.Current?.CurrentTime ?? float.NaN, 1)} elephant={Format(elephant.Position)} " +
            $"realV={HowdahDiagnostics.Format(real.Length, 2)} legsV={HowdahDiagnostics.Format(legs.Length, 2)} carriedV={HowdahDiagnostics.Format(carried, 2)} " +
            $"moveV={HowdahDiagnostics.Format(elephant.MovementVelocity.Length, 2)} drift={HowdahDiagnostics.Format(drift, 3)} " +
            $"floorClearance={HowdahDiagnostics.Format(clearance, 3)} path={placement} seated={seated}/{seats.Count} " +
            $"action={elephant.GetCurrentAction(0).GetName()}");
    }

    private static float CapsuleTop(Agent elephant)
    {
        Monster monster = elephant.Monster;
        if (monster == null) return float.NaN;
        return HowdahDiagnostics.CapsuleTopZ(elephant.Position.z, monster.BodyCapsulePoint1.z, monster.BodyCapsulePoint2.z, monster.BodyCapsuleRadius);
    }

    private float FloorClearance(float capsuleTop) =>
        _hasFloor && _floor.IsValid ? HowdahDiagnostics.Clearance(_floor.GlobalPosition.z, capsuleTop) : float.NaN;

    private static string Format(Vec3 v) =>
        $"({HowdahDiagnostics.Format(v.x, 2)},{HowdahDiagnostics.Format(v.y, 2)},{HowdahDiagnostics.Format(v.z, 2)})";
}
