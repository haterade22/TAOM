using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LostFooting.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Engine side of the Lost Footing rescue (#894 phase 2, ADR-007). It moves one agent with <c>Agent.TeleportToPosition</c>,
/// which places the mount, the agent and any rider on the same point and calls OnAgentTeleported on the moved agent's
/// own components, which no vanilla or TAOM component overrides (v1.5.7 Agent.cs:4287-4302).
/// A spot counts only when a lookup at about the agent's height finds the navigation mesh within the step limit
/// (<c>WorldPosition.GetNavMesh</c>, then <c>GetNavMeshZ</c>), is not on a dynamic navigation mesh, and, for a soldier in
/// water, stands above the water there. The census showed why the height test matters: the engine's own exit point is
/// under 1 m away for nearly every stranded soldier, yet a lookup there at his height finds no mesh two times in three,
/// because the mesh is on the wall walk or the ground below. Main thread only (OnMissionTick).
/// </summary>
public sealed class LostFootingRescueAdapter : ILostFootingRescueAdapter
{
    private static readonly float[] Rings = { 0.5f, 1f, 1.5f, 2f, 3f, 4f, 5f, 6f };
    private const int Directions = 12;

    // A spot is dry when the water there is at least this far below it.
    private const float DryMargin = 0.1f;

    // Tries around the formation's order point. A unit without its own slot gets the formation's single point
    // (Formation.cs:1344, :1360-1362), so each move is spread as vanilla's deployment spread does (:1345-1353).
    private const int SpreadTries = 6;

    private readonly Random _random = new Random();
    private readonly Action<string> _warn;

    public LostFootingRescueAdapter(IModLogger logger) => _warn = (logger ?? throw new ArgumentNullException(nameof(logger))).LogWarning;

    public RescueResult MoveNear(object handle, float maxMeters, float maxStep, bool needLand)
    {
        if (!TryAgent(handle, out var mission, out var agent, out var why)) return RescueResult.Refused(why);

        var from = agent.Position;
        foreach (var radius in Rings)
        {
            if (radius > maxMeters) break;
            for (var i = 0; i < Directions; i++)
            {
                var angle = (float)(2.0 * Math.PI * i / Directions);
                var xy = new Vec2(from.x + radius * (float)Math.Cos(angle), from.y + radius * (float)Math.Sin(angle));
                if (TryStand(mission, xy, from.z, maxStep, needLand, out var spot))
                    return Move(agent, spot);
            }
        }

        return RescueResult.Refused("no-standing-spot");
    }

    public RescueResult MoveToFormation(object handle, float minPlayerMeters, bool needLand)
    {
        if (!TryAgent(handle, out var mission, out var agent, out var why)) return RescueResult.Refused(why);
        if (agent.Formation == null) return RescueResult.Refused("no-formation");

        var order = agent.Formation.GetOrderPositionOfUnit(agent);
        if (!order.IsValid) return RescueResult.Refused("no-formation-spot");

        var main = Agent.Main;
        if (main != null && main.IsActive())
        {
            var player = main.Position.AsVec2;
            if (!(player.Distance(agent.Position.AsVec2) >= minPlayerMeters) || !(player.Distance(order.AsVec2) >= minPlayerMeters))
                return RescueResult.Refused(RescueResult.PlayerNear);
        }

        var z = order.GetNavMeshZ();
        if (float.IsNaN(z) || float.IsInfinity(z)) return RescueResult.Refused("spot-off-mesh");

        var unplaced = agent.Formation.Arrangement?.GetUnpositionedUnits()?.Count ?? 0;
        var spread = (float)Math.Max(1.0, Math.Sqrt(unplaced));
        for (var i = 0; i < SpreadTries; i++)
        {
            var angle = _random.NextDouble() * 2.0 * Math.PI;
            var radius = spread * (float)Math.Sqrt(_random.NextDouble());
            var xy = new Vec2(order.AsVec2.x + radius * (float)Math.Cos(angle), order.AsVec2.y + radius * (float)Math.Sin(angle));
            if (TryStand(mission, xy, z, 0.5f, needLand, out var spreadSpot)) return Move(agent, spreadSpot);
        }

        if (!TryStand(mission, order.AsVec2, z, 0.5f, needLand, out var spot)) return RescueResult.Refused(needLand ? "spot-not-dry" : "spot-off-mesh");
        return Move(agent, spot);
    }

    private static bool TryAgent(object handle, out Mission mission, out Agent agent, out string why)
    {
        mission = Mission.Current!;
        agent = (handle as Agent)!;
        why = string.Empty;
        if (mission?.Scene == null) why = "no-mission";
        else if (agent == null || !AgentSlotIdentity.IsCurrentOccupant(agent) || !agent.IsActive()) why = "gone";
        else if (agent.IsPlayerControlled || agent.IsMainAgent) why = "player";
        else if (agent.IsRunningAway) why = "router";
        else if (agent.IsUsingGameObject) why = "using-object";
        // A ladder queue, a Long Defeat post or a Fords hold keeps a scripted point the agent walks back to (Agent.cs:2752).
        else if (agent.GetScriptedFlags() != Agent.AIScriptedFrameFlags.None) why = "scripted";
        else if (agent.IsMount && agent.RiderAgent != null) why = "ridden";
        else if (mission.Scene.IsPositionOnADynamicNavMesh(agent.Position)) why = "dynamic-mesh";
        return why.Length == 0;
    }

    private static bool TryStand(Mission mission, Vec2 xy, float height, float maxStep, bool needLand, out Vec3 spot)
    {
        spot = Vec3.Invalid;
        var scene = mission.Scene;
        var probe = new WorldPosition(scene, new Vec3(xy.x, xy.y, height));
        if (probe.GetNavMesh() == UIntPtr.Zero) return false;

        var z = probe.GetNavMeshZ();
        if (float.IsNaN(z) || float.IsInfinity(z) || !(Math.Abs(z - height) <= maxStep)) return false;

        var at = new Vec3(xy.x, xy.y, z);
        if (!mission.IsPositionInsideBoundaries(xy)) return false;
        if (scene.IsPositionOnADynamicNavMesh(at)) return false;
        if (needLand)
        {
            var water = mission.GetWaterLevelAtPosition(xy, true);
            if (!float.IsNaN(water) && water > z - DryMargin) return false;
        }

        spot = at;
        return true;
    }

    private RescueResult Move(Agent agent, Vec3 spot)
    {
        MissionThreadGuard.NoteCall("LostFootingRescueAdapter.Move", _warn);
        var from = agent.Position;
        agent.TeleportToPosition(spot);
        return new RescueResult { Moved = true, Meters = from.AsVec2.Distance(spot.AsVec2), ToX = spot.x, ToY = spot.y, ToZ = spot.z };
    }
}
