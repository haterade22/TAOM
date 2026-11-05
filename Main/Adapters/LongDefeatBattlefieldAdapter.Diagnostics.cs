using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

internal sealed partial class LongDefeatBattlefieldAdapter
{
    public LdReading Read(LdPoint? keep, float radius, float heightBand, bool countKeep)
    {
        int attackers = 0, fleeing = 0, atKeep = 0, attackersHigh = 0;
        foreach (var agent in _mission.AttackerTeam.ActiveAgents)
        {
            if (!agent.IsHuman || !agent.IsActive())
                continue;
            if (agent.IsRunningAway)
                fleeing++;
            else
                attackers++;
            if (agent.Position.z >= _highZ)
                attackersHigh++;
            if (!countKeep || agent.IsRunningAway)
                continue;
            if (AtKeep(agent, keep, radius, heightBand))
                atKeep++;
        }

        int defenders = 0, defendersFleeing = 0, defendersAtKeep = 0, defendersHigh = 0;
        foreach (var agent in _mission.DefenderTeam.ActiveAgents)
        {
            if (!agent.IsHuman || !agent.IsActive())
                continue;
            if (agent.IsRunningAway)
            {
                defendersFleeing++;
                continue;
            }

            defenders++;
            if (agent.Position.z >= _highZ)
                defendersHigh++;
            if (countKeep && AtKeep(agent, keep, radius, heightBand))
                defendersAtKeep++;
        }

        return new LdReading(attackers, fleeing, defenders, atKeep, defendersAtKeep, MainBody(), BannerlordConfig.GetRealBattleSizeForSiege(),
            defendersFleeing, attackersHigh, defendersHigh);
    }

    public IReadOnlyList<LdPoint> AttackersNearest(LdPoint? keep) =>
        _mission.AttackerTeam.ActiveAgents
            .Where(a => a.IsHuman && a.IsActive() && !a.IsRunningAway)
            .OrderBy(a => keep is { } k ? DistanceSquared(a.Position, k) : 0f)
            .Take(SampleSize)
            .Select(a => Point(a.Position))
            .ToList();

    private static float DistanceSquared(Vec3 at, LdPoint k)
    {
        var dx = at.x - k.X;
        var dy = at.y - k.Y;
        return dx * dx + dy * dy;
    }

    private static float HighLine(CastleGate? gate, IReadOnlyCollection<WallSegment> walls) =>
        walls.Count > 0 ? walls.Max(w => w.GameEntity.GlobalPosition.z) - WallMargin : (gate != null ? gate.GameEntity.GlobalPosition.z : 0f) + GateToWallFallback;

    public string DescribePlayer()
    {
        var main = _mission.MainAgent;
        if (main == null)
            return "main=none";

        var owner = _mission.DefenderTeam?.PlayerOrderController?.Owner;
        return $"main={main.Name} owner={owner?.Name?.ToString() ?? "null"} controller={main.Controller} z={Num(main.Position.z)} at={At(main.Position)}";
    }

    public string DescribeFormations()
    {
        var all = _mission.DefenderTeam?.FormationsIncludingEmpty.ToList() ?? new List<Formation>();
        return $"ai_formations={all.Count(f => f.IsAIControlled)} of {all.Count}";
    }

    private static bool AtKeep(Agent agent, LdPoint? keep, float radius, float heightBand)
    {
        if (keep is not { } k)
            return false;
        var at = agent.Position;
        var dx = at.x - k.X;
        var dy = at.y - k.Y;
        return dx * dx + dy * dy <= radius * radius && System.Math.Abs(at.z - k.Z) <= heightBand;
    }

    private int MainBody()
    {
        var main = _mission.MainAgent;
        if (main == null || !main.IsActive())
        {
            _lastMain = null;
            return 0;
        }

        if (!ReferenceEquals(main, _lastMain))
        {
            _lastMain = main;
            _knownMain = main;
            _bodySequence++;
        }

        return _bodySequence;
    }

    public LdBootFacts BootFacts()
    {
        var main = _mission.MainAgent;
        var defenders = _mission.DefenderTeam;
        var siegeAi = defenders?.TeamAI is TeamAISiegeDefender && _mission.AttackerTeam?.TeamAI is TeamAISiegeAttacker;
        return new LdBootFacts(
            _mission.IsDeploymentFinished,
            siegeAi,
            main != null,
            _mission.IsFriendlyMission,
            main != null && ReferenceEquals(defenders?.GeneralAgent, main),
            defenders?.IsPlayerGeneral == true,
            main != null && ReferenceEquals(defenders?.PlayerOrderController?.Owner, main));
    }

    public string DescribeEngines()
    {
        var ladders = _ladders.Select(l => l.State.ToString());
        var towers = _towers.Select(t => $"{t.HasArrivedAtTarget}/{t.IsDestroyed}/{t.ForcedUse}");
        var rams = _rams.Select(r => $"{r.HasArrivedAtTarget}/{r.IsDestroyed}/{r.HasCompletedAction()}/{r.ForcedUse}");
        var gates = _gates.Select(g =>
        {
            var c = g.GameEntity.GetFirstScriptOfType<DestructableComponent>();
            return $"{Num(c?.HitPoint ?? -1f)} {g.State} destroyed {g.IsDestroyed} disabled {g.IsDisabled}";
        });
        var lanes = TeamAISiegeComponent.SiegeLanes?.Select(l => $"{l.LaneSide} open {l.IsOpen} breach {l.IsBreach}") ?? Enumerable.Empty<string>();
        return $"[Engines] ladders [{string.Join(",", ladders)}] towers arrived/destroyed/forced [{string.Join(",", towers)}] "
            + $"rams arrived/destroyed/done/forced [{string.Join(",", rams)}] gates [{string.Join("; ", gates)}] lanes [{string.Join("; ", lanes)}]";
    }

    // An attacker's removal is the last event for him, so his role and felled mark are taken from the registry and he is forgotten. Only the commander is reported when no loss
    // came with the removal (a rout; a deletion only if native raises OnAgentRemoved for it, risk 2, since the managed OnAgentDeleted, Mission.cs:2982-2996, reaches no Long Defeat callback), because a commander who leaves alive stops summoning; any other attacker's non-loss removal is nothing.
    public LdRemoval Classify(object affected, bool loss)
    {
        if (!(affected is Agent agent) || !agent.IsHuman || !ReferenceEquals(agent.Mission, _mission))
            return new LdRemoval(LdRemovalKind.None, "");

        var team = agent.Team;
        if (team == null)
            return new LdRemoval(LdRemovalKind.None, "");

        var id = agent.Character?.StringId ?? "";
        if (team.Side == BattleSideEnum.Attacker)
        {
            _registry.TakeRemoval(agent, out var role, out var felled);
            if (loss)
                return new LdRemoval(LdRemovalKind.Attacker, id, role, felled);

            return role == AttackerRole.Commander ? new LdRemoval(LdRemovalKind.Attacker, id, role, false, false) : new LdRemoval(LdRemovalKind.None, "");
        }

        if (!loss)
            return new LdRemoval(LdRemovalKind.None, "");

        // The engine nulls MainAgent around this callback, so the body the adapter last saw is the player's too.
        var isPlayer = agent == _mission.MainAgent || ReferenceEquals(agent, _knownMain) || agent.Controller == AgentControllerType.Player;
        return new LdRemoval(isPlayer ? LdRemovalKind.Player : LdRemovalKind.Defender, id);
    }

    public bool IsAttacker(object? agent) =>
        agent is Agent a && ReferenceEquals(a.Mission, _mission) && a.Team != null && a.Team.Side == BattleSideEnum.Attacker;
}
