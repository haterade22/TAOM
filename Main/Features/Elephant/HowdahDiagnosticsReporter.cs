using System.Collections.Generic;
using System.Text;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
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
    public void AfterReposition(string tag, WeakGameEntity platform, Agent elephant, IReadOnlyList<StandingPoint> seats, string placement,
        string deckNavMeshName = "", int deckNavMeshIdStart = 0)
    {
        if (!_sampleDue) return;
        _sampleDue = false;
        if (!_layoutLogged)
        {
            LogLayout(tag, platform, elephant, seats);
            _logger.LogInfo(
                $"{tag} deck navmesh: name='{deckNavMeshName}' idStart={deckNavMeshIdStart} " +
                (string.IsNullOrEmpty(deckNavMeshName)
                    ? "(none: the crew stand on no navigation face, which is what nav=0 in the status line means)"
                    : "(imported by MissionObject.OnInit and attached to this entity)"));
        }
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
            $"floorOrigin={(_hasFloor ? Format(_floor.GlobalPosition) : "missing")} floorClearance={HowdahDiagnostics.Format(clearance, 3)} " +
            $"scale={HowdahDiagnostics.Format(elephant.AgentScale, 2)} {SpineProbe(elephant, seats)}");

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
        int detached = 0;
        int ranged = 0;
        string formationName = "none";
        var crew = new StringBuilder();
        for (int i = 0; i < seats.Count; i++)
        {
            Agent rider = seats[i].MovingAgent;
            if (rider == null) continue;
            // A dead archer's index goes to the next agent built, and its handle then answers for that stranger
            // (#592). This log is the evidence these decisions are made from, so a misattributed row is worse than a
            // missing one.
            if (!AgentSlotIdentity.IsCurrentOccupant(rider))
            {
                crew.Append($" seat{i}=stale-handle");
                continue;
            }
            var seat = seats[i] as TaomHowdahStandingPoint;
            seated++;
            if (rider.IsDetachedFromFormation) detached++;
            if (rider.Formation != null)
            {
                ranged++;
                formationName = rider.Formation.FormationIndex.ToString();
            }
            Vec3 seatPos = seats[i].GameEntity.GlobalPosition;
            Vec3 riderPos = rider.Position;
            float gap = HowdahDiagnostics.Distance(riderPos.x, riderPos.y, riderPos.z, seatPos.x, seatPos.y, seatPos.z);
            // Both channels: 0 is the body action, 1 the upper body, which is where a bow draw and release play. A
            // seat that only ever reads act_none on both is an archer the engine is not letting shoot (#627).
            // mr is Agent.MissileRangeAdjusted: 0 means the engine thinks this archer can reach nothing (it returns
            // 0f for a null Formation, and for one whose closest enemy formation has not been cached). fire is
            // GetFiringOrder: 0 FireAtWill, 1 HoldYourFire. Between them a single battle says which gate is shut.
            crew.Append($" seat{i}={rider.GetCurrentAction(0).GetName()}/{rider.GetCurrentAction(1).GetName()}" +
                        $"@{HowdahDiagnostics.Format(gap, 2)}m" +
                        $" mr={HowdahDiagnostics.Format(rider.MissileRangeAdjusted, 1)}" +
                        $" fire={rider.GetFiringOrder()}" +
                        // prog is the highest upper-body action progress seen since the mission started and restarts
                        // counts how often it went backwards: a draw that is being reset every frame never gets far
                        // and restarts climbs with the frame rate, while a draw that completes reaches 1 and restarts
                        // stays near the number of shots. ammo answers "ranged weapon WITH ammunition", tgt whether
                        // the engine has given this archer someone to shoot, nav the navmesh face under its feet
                        // (0 or -1 means the deck has none, which is the one thing vanilla's moving platforms do have).
                        $" prog={HowdahDiagnostics.Format(seat?.MaxActionProgress ?? float.NaN, 2)}" +
                        $" restarts={seat?.ActionRestarts ?? -1}" +
                        // Arrows actually loosed. Read against restarts: many restarts and few shots is the re-nock
                        // loop; restarts rising with shots is an archer shooting normally, since every shot restarts.
                        $" shots={(rider.Origin as HowdahCrewAgentOrigin)?.ShotsFired ?? -1}" +
                        $" ammo={rider.HasRangedWeapon(true)}" +
                        $" tgt={(rider.GetTargetAgent() != null)}" +
                        $" nav={rider.GetCurrentNavigationFaceId()}" +
                        // The archer's OWN movement as the engine sees it, and the elephant's navmesh face as a
                        // control: a foot archer that reads as travelling at the elephant's speed, or one standing on
                        // no navmesh face where its mount has one, are the two remaining reasons a drawn shot is
                        // refused at the release (#627).
                        $" aV={HowdahDiagnostics.Format(rider.GetAverageRealGlobalVelocity().Length, 2)}" +
                        $" legV={HowdahDiagnostics.Format(rider.AverageVelocity.Length, 2)}" +
                        $" enav={elephant.GetCurrentNavigationFaceId()}");
        }

        _logger.LogInfo(
            $"{tag} status t={HowdahDiagnostics.Format(Mission.Current?.CurrentTime ?? float.NaN, 1)} elephant={Format(elephant.Position)} " +
            $"realV={HowdahDiagnostics.Format(real.Length, 2)} legsV={HowdahDiagnostics.Format(legs.Length, 2)} carriedV={HowdahDiagnostics.Format(carried, 2)} " +
            $"moveV={HowdahDiagnostics.Format(elephant.MovementVelocity.Length, 2)} drift={HowdahDiagnostics.Format(drift, 3)} " +
            $"floorClearance={HowdahDiagnostics.Format(clearance, 3)} path={placement} seated={seated}/{seats.Count} " +
            $"formation={formationName}({ranged}/{seated}) detached={detached}/{seated} " +
            $"action={elephant.GetCurrentAction(0).GetName()} {SpineProbe(elephant, seats)}{crew}");
    }

    // The visible howdah is a HorseHarness mesh skinned to the elephant's spine, so where the spine bone actually is
    // says where the deck the player sees actually is. At 1.0x the elite deck sat 0.951 m above Spine1_05's rest
    // head (2.199 m on elephant_skeleton), so deckShouldBe is that offset scaled from the LIVE bone. If it disagrees
    // with the platform's own deck height, the platform is at the wrong height and this number is how far.
    // Index-based and bounds-checked, never the by-name frame API, which faults on a missing name.
    private string SpineProbe(Agent elephant, IReadOnlyList<StandingPoint> seats)
    {
        try
        {
            var visuals = elephant.AgentVisuals;
            Skeleton skel = visuals?.GetSkeleton();
            if (skel == null) return "spine=noskeleton";
            int count = skel.GetBoneCount();
            sbyte idx = TaomHowdahMachine.ResolveBoneIndex(skel, "Spine1_05", count);
            if (idx < 0 || idx >= count) return $"spine=missing(count={count})";
            MatrixFrame world = visuals.GetGlobalFrame().TransformToParent(skel.GetBoneEntitialFrameWithIndex(idx));
            float spine = world.origin.z - elephant.Position.z;
            float deckShouldBe = spine + 0.951f * elephant.AgentScale;
            // Where the platform ACTUALLY put its deck this frame, read off the first seat, not a constant: since the
            // platform follows the spine, the fixed fallback height no longer says where it is.
            float seatDeck = float.NaN;
            for (int i = 0; i < seats.Count; i++)
                if (seats[i]?.GameEntity.IsValid == true)
                {
                    seatDeck = seats[i].GameEntity.GlobalPosition.z - elephant.Position.z;
                    break;
                }
            return $"spineZ={HowdahDiagnostics.Format(spine, 2)} deckShouldBe={HowdahDiagnostics.Format(deckShouldBe, 2)} " +
                   $"seatDeck={HowdahDiagnostics.Format(seatDeck, 2)} " +
                   $"raiseBy={HowdahDiagnostics.Format(deckShouldBe - seatDeck, 2)}";
        }
        catch (System.Exception ex)
        {
            return "spine=error:" + ex.GetType().Name;
        }
    }

    private static float CapsuleTop(Agent elephant)
    {
        Monster monster = elephant.Monster;
        if (monster == null) return float.NaN;
        return HowdahDiagnostics.CapsuleTopZ(elephant.Position.z, monster.BodyCapsulePoint1.z, monster.BodyCapsulePoint2.z,
            monster.BodyCapsuleRadius, elephant.AgentScale);
    }

    private float FloorClearance(float capsuleTop) =>
        _hasFloor && _floor.IsValid ? HowdahDiagnostics.Clearance(_floor.GlobalPosition.z, capsuleTop) : float.NaN;

    private static string Format(Vec3 v) =>
        $"({HowdahDiagnostics.Format(v.x, 2)},{HowdahDiagnostics.Format(v.y, 2)},{HowdahDiagnostics.Format(v.z, 2)})";
}
