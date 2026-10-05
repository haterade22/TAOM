using System;
using System.Collections.Generic;
using HarmonyLib;
using TAOM.Core.Validation;
using TAOM.Features.CreatureSiegeRole.Domain;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// The wall battle for the creature siege role, over the siege team AI, the gates, the ram, the ladders and towers and the
/// mission's agent list (v1.5.4). One per mission, built by the mission behavior inside <c>AfterStart</c>'s try and used on the
/// main thread only. Reads throw on an engine fault and the role service catches them (its fail-safe), so nothing here swallows.
/// Members referenced here are pinned by the binding tests, because a member that stops resolving fails when its method is first
/// compiled, before any try around the call can run.
/// </summary>
public sealed class CreatureSiegeMissionAdapter : ICreatureSiegeMissionAdapter
{
    private const string OuterGateTag = "outer_gate";
    private const string InnerGateTag = "inner_gate";
    private const string MiddlePositionTag = "middle_pos";

    // The ground probe starts this far above the hint, as vanilla's own wheel probe starts above its wheel
    // (SiegeWeaponMovementComponent): ground a step above a gate's base is found, a gatehouse roof is not.
    private const float GroundProbeLift = 1.5f;

    // How far below its start the ground probe looks. A scene's gate ground is never this far under a gate's base.
    private const float GroundProbeDepth = 100f;

    // MissionObject.DynamicNavmeshIdStart is protected. Bound once and fail-soft: a field that stops resolving on a later
    // engine makes every tower read as "no navmesh" (start 0), which the role rules skip with one named warning each, instead of
    // a TypeInitializationException out of the first read.
    private static readonly AccessTools.FieldRef<MissionObject, int>? DynamicNavmeshIdStart = BindDynamicNavmeshIdStart();

    private readonly Mission _mission;
    private readonly List<ICreatureSiegeAgentAdapter> _creatures = new();

    public CreatureSiegeMissionAdapter(Mission mission)
    {
        _mission = mission ?? throw new ArgumentNullException(nameof(mission));
    }

    public object MissionToken => _mission;

    public string SceneName => _mission.SceneName ?? string.Empty;

    public float Time => _mission.CurrentTime;

    public bool IsSiegeBattle => _mission.IsSiegeBattle;

    // A sally-out and a relief force both carry a controller derived from this one (SandBoxMissions, CustomBattle missions).
    public bool HasSallyOutController => _mission.GetMissionBehavior<SallyOutMissionController>() != null;

    public bool IsClientOrReplay => GameNetwork.IsClientOrReplay;

    public bool IsDeploymentFinished => _mission.IsDeploymentFinished;

    public IReadOnlyList<SiegeGateReading> OuterGateCandidates()
    {
        var siege = SiegeTeamAi();
        return siege == null ? Array.Empty<SiegeGateReading>() : GatesTagged(OuterGateTag, siege.OuterGate);
    }

    public IReadOnlyList<SiegeGateReading> InnerGateCandidates()
    {
        var siege = SiegeTeamAi();
        return siege == null ? Array.Empty<SiegeGateReading>() : GatesTagged(InnerGateTag, siege.InnerGate);
    }

    public GateLiveState ReadGate(object gate)
    {
        if (gate is not CastleGate castleGate)
            throw new ArgumentException("A gate read needs the gate handle this adapter made.", nameof(gate));

        var destruction = castleGate.DestructionComponent;
        return new GateLiveState(castleGate.IsDestroyed, castleGate.State == CastleGate.GateState.Open && !castleGate.IsDestroyed,
            destruction == null ? float.NaN : destruction.HitPoint);
    }

    public RamReading ReadRam()
    {
        var siege = SiegeTeamAi();
        if (siege == null) return default;

        foreach (var weapon in siege.PrimarySiegeWeapons)
        {
            if (weapon is BatteringRam ram)
                return new RamReading(true, ram.IsDeactivated, ram.UserCountIncludingInStruckAction, ram.IsUsed);
        }

        return default;
    }

    public IReadOnlyList<int> ReadLadderWallIds()
    {
        var ids = new List<int>();
        var siege = SiegeTeamAi();
        if (siege == null) return ids;

        foreach (var ladder in siege.Ladders)
        {
            if (ladder != null && !ladder.IsDisabled)
                ids.Add(ladder.OnWallNavMeshId);
        }

        return ids;
    }

    public IReadOnlyList<TowerFaces> ReadTowers()
    {
        var towers = new List<TowerFaces>();
        var siege = SiegeTeamAi();
        if (siege == null) return towers;

        foreach (var weapon in siege.PrimarySiegeWeapons)
        {
            if (weapon is SiegeTower tower && !tower.IsDisabled)
                towers.Add(new TowerFaces(DynamicStartOf(tower), tower.GetGateNavMeshId()));
        }

        return towers;
    }

    public float GroundHeight(float x, float y, float heightHint)
    {
        if (!FiniteFloatValidator.IsFinite(x) || !FiniteFloatValidator.IsFinite(y) || !FiniteFloatValidator.IsFinite(heightHint))
            return float.NaN;

        // A downward ray, because the native height query answers 0 on a miss, which is a plausible height. The ray's hit flag
        // is what tells a miss, and the body flags are the height query's own default.
        var top = new Vec3(x, y, heightHint + GroundProbeLift);
        var bottom = new Vec3(x, y, top.z - GroundProbeDepth);
        Vec3 ground;
        var hit = _mission.Scene.RayCastForClosestEntityOrTerrain(top, bottom, out _, out ground,
            excludeBodyFlags: BodyFlags.CommonCollisionExcludeFlags);
        return hit && FiniteFloatValidator.IsFinite(ground.z) ? ground.z : float.NaN;
    }

    // Built from the live active list on every call, so no adapter outlives its pass. A mount has no Character and so no race.
    public IReadOnlyList<ICreatureSiegeAgentAdapter> CollectCreatures(Func<int, bool> isCreatureRace)
    {
        _creatures.Clear();

        var scene = _mission.Scene;
        var agents = _mission.Agents;
        for (var i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            var character = agent?.Character;
            if (character == null || !isCreatureRace(character.Race)) continue;

            _creatures.Add(new CreatureSiegeAgentAdapter(agent!, scene));
        }

        return _creatures;
    }

    // Both siege team AIs derive from TeamAISiegeComponent and find the gates the same way, so either one answers.
    private TeamAISiegeComponent? SiegeTeamAi() =>
        _mission.AttackerTeam?.TeamAI as TeamAISiegeComponent ?? _mission.DefenderTeam?.TeamAI as TeamAISiegeComponent;

    // The engine's own choice first (the first tagged gate in the active list, hidden or not), then every other tagged gate in
    // list order: TAOM scenes hide duplicate inner gates, and the role steps over an unusable one to the next.
    private List<SiegeGateReading> GatesTagged(string tag, CastleGate? canonical)
    {
        var readings = new List<SiegeGateReading>();
        if (canonical != null)
            readings.Add(Read(canonical));

        foreach (var missionObject in _mission.ActiveMissionObjects)
        {
            if (missionObject is CastleGate gate && !ReferenceEquals(gate, canonical) && HasTag(gate, tag))
                readings.Add(Read(gate));
        }

        return readings;
    }

    private static bool HasTag(CastleGate gate, string tag)
    {
        var entity = gate.GameEntity;
        return entity.IsValid && entity.HasTag(tag);
    }

    private SiegeGateReading Read(CastleGate gate)
    {
        var entity = gate.GameEntity;
        var destruction = gate.DestructionComponent;
        if (!entity.IsValid)
        {
            const float nan = float.NaN;
            return new SiegeGateReading(gate, "invalid", false, gate.IsDisabled, nan, nan, nan, nan, nan, false, nan, nan, nan,
                destruction);
        }

        var frame = entity.GetGlobalFrame();
        var forward = frame.rotation.f;

        // The first middle_pos child, full depth with no visibility filter: the one CastleGate.OnInit takes. A gate without
        // one offers no middle anchor, because its own frame is the doorway.
        var hasMiddle = false;
        var middleX = float.NaN;
        var middleY = float.NaN;
        var middleZ = float.NaN;
        var middles = entity.CollectChildrenEntitiesWithTag(MiddlePositionTag);
        if (middles.Count > 0)
        {
            var at = middles[0].GlobalPosition;
            hasMiddle = true;
            middleX = at.x;
            middleY = at.y;
            middleZ = GroundHeight(at.x, at.y, at.z);
        }

        return new SiegeGateReading(gate, entity.Name ?? string.Empty, entity.IsVisibleIncludeParents(), gate.IsDisabled,
            frame.origin.x, frame.origin.y, frame.origin.z, forward.x, forward.y, hasMiddle, middleX, middleY, middleZ, destruction);
    }

    private static int DynamicStartOf(SiegeTower tower) => DynamicNavmeshIdStart == null ? 0 : DynamicNavmeshIdStart(tower);

    private static AccessTools.FieldRef<MissionObject, int>? BindDynamicNavmeshIdStart()
    {
        try
        {
            return AccessTools.FieldRefAccess<MissionObject, int>("DynamicNavmeshIdStart");
        }
        catch (Exception)
        {
            return null;
        }
    }
}
