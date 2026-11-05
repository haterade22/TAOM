using System;
using System.Collections.Generic;
using BehaviorTreeWrapper;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LostFooting.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Engine side of the Lost Footing census (ADR-007). READ ONLY: nothing here moves, pauses, orders or otherwise
/// changes an agent or the mission, and a source test pins that. It runs on the main thread from
/// <c>OnMissionTick</c>. Engine members (v1.5.5; re-read on v1.4.8 by plan 117): <c>Mission.Agents/SceneName/GetWaterLevelAtPosition/IsFieldBattle/
/// IsSiegeBattle/IsSallyOutBattle</c>, <c>Scene.GetWaterLevel/IsPositionOnADynamicNavMesh/
/// FindClosestExitPositionForPositionOnABoundaryFace</c>, <c>Agent.GetWorldPosition/Position/GetCurrentVelocity/
/// GetAIMoveDestination/GetAgentFlags/IsActive/IsPlayerControlled/IsHuman/IsMount/RiderAgent/Formation/IsRunningAway/IsUsingGameObject/
/// IsOnLand/GetComponent</c>, <c>WorldPosition.GetNavMesh/GetNearestNavMesh/IsValid/AsVec2</c>,
/// <c>Formation.GetOrderPositionOfUnit</c>. The off-mesh test is the agent's own world position having no navigation
/// mesh; <c>GetCurrentNavigationFaceId</c> is not used because face id 0 collides with face group 0. The goal is
/// <c>GetAIMoveDestination</c>, not <c>GetTargetPosition</c> (the movement-lock target, (0,0) for an ordinary soldier).
/// </summary>
public sealed class LostFootingAdapter : ILostFootingAdapter
{
    // Under this speed (m/s), or off the mesh, an agent gets the costly reads: the water level, its goal and its formation
    // spot. A moving agent cannot be stranded, so it costs two native reads only. GetOrderPositionOfUnit also prints a
    // warning for a charge order with no valid position, which this keeps rare.
    private const float SlowEnoughToReadMore = 2f;

    public SceneFacts ReadScene()
    {
        var mission = Mission.Current;
        var facts = new SceneFacts();
        if (mission == null) return facts;

        facts.SceneId = mission.SceneName ?? string.Empty;
        facts.MissionType = mission.IsFieldBattle ? "field" : mission.IsSiegeBattle ? "siege" : mission.IsSallyOutBattle ? "sally-out" : "other";
        facts.WaterLevel = mission.Scene != null ? mission.Scene.GetWaterLevel() : float.NaN;
        return facts;
    }

    public int AgentCount => Mission.Current?.Agents.Count ?? 0;

    public void ReadSlice(int start, int max, List<AgentProbe> into)
    {
        var mission = Mission.Current;
        if (mission == null) return;

        var agents = mission.Agents;
        var end = Math.Min(agents.Count, start + max);
        for (var i = Math.Max(0, start); i < end; i++)
        {
            var agent = agents[i];
            if (agent == null || !agent.IsActive()) continue;

            // A mount with a rider is the rider's business; only a mount with nobody on it is its own case.
            if (agent.IsMount && agent.RiderAgent != null) continue;

            into.Add(Probe(mission, agent));
        }
    }

    private static AgentProbe Probe(Mission mission, Agent agent)
    {
        var position = agent.Position;
        var speed = agent.GetCurrentVelocity().Length;
        var probe = new AgentProbe
        {
            Handle = agent,
            IsPlayerControlled = agent.IsPlayerControlled,
            IsHuman = agent.IsHuman,
            InFormation = agent.Formation != null,
            IsRouter = agent.IsRunningAway,
            IsMount = agent.IsMount,
            // A built behavior tree marks one of TAOM's creatures (a failed build leaves Tree null).
            IsCreature = agent.GetComponent<BehaviorTreeAgentComponent>() is { } tree && tree.Tree != null,
            IsUsingObject = agent.IsUsingGameObject,
            // Not Land is the test the engine uses to withhold the formation frame (Agent.cs:2089).
            InWaterMode = !agent.IsOnLand(),
            FaceMiss = agent.GetWorldPosition().GetNavMesh() == UIntPtr.Zero,
            Unreachable = (agent.GetAgentFlags() & AgentFlag.UnreachableViaNavMesh) != 0,
            X = position.x,
            Y = position.y,
            Z = position.z,
            Speed = speed,
        };

        if (speed < SlowEnoughToReadMore || probe.FaceMiss)
        {
            probe.WaterLevelHere = mission.GetWaterLevelAtPosition(position.AsVec2, true);
            var destination = agent.GetAIMoveDestination();
            probe.DistToDestination = destination.IsValid ? Distance(position.AsVec2, destination.AsVec2) : float.NaN;
            var order = agent.Formation?.GetOrderPositionOfUnit(agent) ?? WorldPosition.Invalid;
            probe.DistToFormation = order.IsValid ? Distance(position.AsVec2, order.AsVec2) : float.NaN;
        }

        return probe;
    }

    private static float Distance(Vec2 from, Vec2 to) => from.IsValid && to.IsValid ? from.Distance(to) : float.NaN;

    public OffMeshDetail Confirm(object handle)
    {
        var detail = new OffMeshDetail();
        var mission = Mission.Current;
        var scene = mission?.Scene;
        if (scene == null || !(handle is Agent agent) || !AgentSlotIdentity.IsCurrentOccupant(agent) || !agent.IsActive()) return detail;

        // A fresh position has not been told which face it is on, so asking for its mesh does a real lookup.
        var position = agent.Position;
        var fresh = new WorldPosition(scene, position);
        detail.OnDynamicNavMesh = scene.IsPositionOnADynamicNavMesh(position);
        detail.Confirmed = fresh.GetNavMesh() == UIntPtr.Zero && !detail.OnDynamicNavMesh;
        if (!detail.Confirmed) return detail;

        // The engine's snap pair (Agent.GetBaseFormationFrame, which uses it to repair the formation position, not the
        // agent): nearest mesh, then the closest exit on its boundary face. Asked only of a confirmed agent.
        var nearest = fresh.GetNearestNavMesh();
        if (nearest == UIntPtr.Zero) return detail;
        var exit = scene.FindClosestExitPositionForPositionOnABoundaryFace(position, nearest);
        if (!exit.IsValid) return detail;

        detail.HasSnap = true;
        detail.SnapDistance = position.AsVec2.Distance(exit);
        // A lookup at the exit point seeded with the agent's own height: what a rescue seeded with his height would find.
        var candidate = new WorldPosition(scene, new Vec3(exit.x, exit.y, position.z));
        detail.SnapHasNavMesh = candidate.GetNavMesh() != UIntPtr.Zero;
        return detail;
    }
}
