using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters.Models;

namespace TAOM.Adapters;

public sealed class BattlefieldQueryAdapter : IBattlefieldQueryAdapter
{
    // Per-instance, NOT static. The adapter is registered as Reuse.Singleton so there's
    // exactly one instance, but a per-instance buffer means a future hypothetical
    // re-entrant call (or a second adapter created for a test) doesn't corrupt shared state.
    private readonly MBList<Agent> _scratchBuffer = new();

    public bool HasPlayerTeam => Mission.Current?.PlayerTeam != null;

    // Mission.IsFieldBattle => MissionTeamAIType == FieldBattle (v1.4.7 Mission.cs:1373). A plain
    // managed auto-property read, not a native computed getter, so it cannot throw once Mission is
    // non-null. ?? false => a null mission is treated as "not a field battle" (suppress the feature).
    public bool IsFieldBattle => Mission.Current?.IsFieldBattle ?? false;

    public object? PlayerTeamKey => Mission.Current?.PlayerTeam;

    public IReadOnlyList<IFormationAdapter> GetFriendlyFormationsExcluding(object excludeFormationKey)
    {
        var team = Mission.Current?.PlayerTeam;
        if (team == null) return System.Array.Empty<IFormationAdapter>();

        var result = new List<IFormationAdapter>();
        foreach (var formation in team.FormationsIncludingEmpty)
        {
            if (formation == null) continue;
            if (ReferenceEquals(formation, excludeFormationKey)) continue;
            if (formation.CountOfUnits == 0) continue;
            result.Add(new FormationAdapter(formation));
        }
        return result;
    }

    public bool TryGetNearestEnemyFormation(object ownFormationKey, out object? targetToken, out Vec2 position)
    {
        targetToken = null;
        position = Vec2.Zero;
        var mission = Mission.Current;
        if (mission == null || ownFormationKey is not Formation own || own.Team == null) return false;

        Formation? best = null;
        var bestDistance = float.MaxValue;
        foreach (var team in mission.Teams)
        {
            // Explicit identity skip: IsFriendOf(self) is true in standard setups, but quirky
            // multi-team custom battles have produced false (lessons/harmony-il.md, identity skips).
            if (team == null || team == own.Team) continue;
            if (team.IsFriendOf(own.Team)) continue;
            foreach (var enemy in team.FormationsIncludingEmpty)
            {
                if (enemy == null || enemy.CountOfUnits == 0) continue;
                if (ReferenceEquals(enemy, own)) continue;
                var d = own.CurrentPosition.Distance(enemy.CurrentPosition);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = enemy;
                }
            }
        }
        if (best == null) return false;
        targetToken = best;
        position = best.CurrentPosition;
        return true;
    }

    public void GetNearbyAgents(Vec2 center, float radius, List<NearbyAgentSnapshot> output)
    {
        output.Clear();
        var mission = Mission.Current;
        if (mission == null) return;

        _scratchBuffer.Clear();
        mission.GetNearbyAgents(center, radius, _scratchBuffer);
        foreach (var agent in _scratchBuffer)
        {
            if (agent == null) continue;
            output.Add(new NearbyAgentSnapshot(
                index: agent.Index,
                position: agent.Position.AsVec2,
                isMounted: agent.HasMount,
                isActive: agent.IsActive(),
                teamKey: agent.Team));
        }
    }

    public float GetGroundHeightAtPosition(Vec3 position)
    {
        var scene = Mission.Current?.Scene;
        if (scene == null) return 0f;
        return scene.GetGroundHeightAtPosition(position, BodyFlags.CommonCollisionExcludeFlags);
    }
}
