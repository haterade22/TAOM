using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;
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

        int defenders = 0, defendersFleeing = 0, defendersAtKeep = 0, defendersHigh = 0, healthRead = 0;
        var healthShares = 0f;
        foreach (var agent in _mission.DefenderTeam.ActiveAgents)
        {
            if (!agent.IsHuman || !agent.IsActive())
                continue;

            // The same people RestDefenders heals (runners too), so the field-care check can read the heal back from the next tick's reading.
            var limit = agent.HealthLimit;
            if (limit > 0f)
            {
                healthShares += agent.Health / limit;
                healthRead++;
            }

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
            defendersFleeing, attackersHigh, defendersHigh, healthRead == 0 ? -1f : healthShares / healthRead, ReadGates());
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
    // came with the removal (a rout; a deletion only if native raises OnAgentRemoved for it, risk 2, since the managed OnAgentDeleted, Mission.cs:2982-2996, reaches only LongDefeatMissionLogic.OnAgentDeleted, which logs the lord's bodies and never reaches Classify), because a commander who leaves alive stops summoning; any other attacker's non-loss removal is nothing.
    public LdRemoval Classify(object affected, bool loss)
    {
        if (!(affected is Agent agent) || !agent.IsHuman || !ReferenceEquals(agent.Mission, _mission))
            return new LdRemoval(LdRemovalKind.None, "");

        // A defender the registry holds (one an attacker struck while a blades wave ran) leaves it here too, and the removal carries its key so the run forgets it. The entry is taken before the
        // team is read: an agent with no team is still evicted, and its key still reaches the run.
        // The body a remount faded is the lord's own removal, in whatever state native reports (the plan check reads FadeOut as setting Routed, and a kill or knock-out would be counted as a death): matched by
        // identity, forgotten by the registry, and no loss. NoteBodyEvent has kept its state for the director to log.
        if (_lordWatch.IsPlanned(agent))
        {
            _registry.TakeRemoval(agent, out _, out _, out var plannedKey);
            return new LdRemoval(LdRemovalKind.None, "", Key: plannedKey);
        }

        var id = agent.Character?.StringId ?? "";
        _registry.TakeRemoval(agent, out var role, out var felled, out var key);
        var team = agent.Team;
        if (team == null)
            return new LdRemoval(LdRemovalKind.None, "", Key: key);

        if (team.Side == BattleSideEnum.Attacker)
        {
            if (loss)
                return new LdRemoval(LdRemovalKind.Attacker, id, role, felled, true, key);

            return role == AttackerRole.Commander ? new LdRemoval(LdRemovalKind.Attacker, id, role, false, false, key) : new LdRemoval(LdRemovalKind.None, "", Key: key);
        }

        if (!loss)
            return new LdRemoval(LdRemovalKind.None, "", Key: key);

        // The engine nulls MainAgent around this callback, so the body the adapter last saw is the player's too.
        var isPlayer = agent == _mission.MainAgent || ReferenceEquals(agent, _knownMain) || agent.Controller == AgentControllerType.Player;
        return new LdRemoval(isPlayer ? LdRemovalKind.Player : LdRemovalKind.Defender, id, Key: key);
    }

    public bool IsAttacker(object? agent) =>
        agent is Agent a && ReferenceEquals(a.Mission, _mission) && a.Team != null && a.Team.Side == BattleSideEnum.Attacker;

    // A hit's two keys, which may be resolved off the main thread (the registry holds one lock). The attacker was registered when the field spawned him; the defender is entered on his first hit and leaves at
    // his removal (Classify). A hit by a body the registry no longer holds, or on an agent that is not a standing human defender, has no keys.
    public bool TryHitKeys(object? affected, object? affector, out AgentKey victim, out AgentKey attacker)
    {
        victim = attacker = default;
        if (!(affected is Agent hurt) || !(affector is Agent hitter) || !hurt.IsHuman || !hitter.IsHuman)
            return false;
        if (!ReferenceEquals(hurt.Mission, _mission) || !ReferenceEquals(hitter.Mission, _mission) || hurt.Team?.Side != BattleSideEnum.Defender || hitter.Team?.Side != BattleSideEnum.Attacker)
            return false;
        if (!_registry.TryGetKey(hitter, out attacker))
            return false;

        victim = _registry.Register(hurt, AttackerRole.Roster);
        return true;
    }
}
