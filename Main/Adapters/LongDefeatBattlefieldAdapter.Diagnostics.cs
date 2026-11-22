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
            // The player's body in a story attack (#1409) is no wave soldier: counted, the wave would never clear while he stands.
            if (!agent.IsHuman || !agent.IsActive() || ReferenceEquals(agent, _mission.MainAgent))
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
            .Where(a => a.IsHuman && a.IsActive() && !a.IsRunningAway && !ReferenceEquals(a, _mission.MainAgent))
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

        // A lord who attacks (#1409) also gets the attackers' foot frame, the place his spawn is meant to be near. inside= is the test MissionBoundaryCrossingHandler makes for the
        // main agent (MissionBoundaryCrossingHandler.cs:210): a body outside it starts the retreat countdown, and the run ends Abandoned.
        var owner = PlayerSideTeam?.PlayerOrderController?.Owner;
        var frameText = PlayerAttacks && FrameOf(_mission.AttackerTeam, FormationClass.Infantry, true) is { } frame ? $" frame={At(frame)}" : "";
        var inside = _mission.IsPositionInsideBoundaries(main.Position.AsVec2) ? "true" : "false";
        return $"main={main.Name} owner={owner?.Name?.ToString() ?? "null"} controller={main.Controller} z={Num(main.Position.z)} at={At(main.Position)}{frameText} inside={inside}";
    }

    // ai_formations= reads the player's own team, as it always has; defender_ai= reads the defenders, whose AI state is the #1370 signal in an attack too.
    public string DescribeFormations()
    {
        var all = PlayerSideTeam?.FormationsIncludingEmpty.ToList() ?? new List<Formation>();
        var defenders = _mission.DefenderTeam?.FormationsIncludingEmpty.ToList() ?? new List<Formation>();
        return $"ai_formations={all.Count(f => f.IsAIControlled)} of {all.Count} defender_ai={defenders.Count(f => f.IsAIControlled)} of {defenders.Count}";
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
        var team = PlayerSideTeam;
        var siegeAi = _mission.DefenderTeam?.TeamAI is TeamAISiegeDefender && _mission.AttackerTeam?.TeamAI is TeamAISiegeAttacker;
        return new LdBootFacts(
            _mission.IsDeploymentFinished,
            siegeAi,
            main != null,
            _mission.IsFriendlyMission,
            main != null && ReferenceEquals(team?.GeneralAgent, main),
            team?.IsPlayerGeneral == true,
            main != null && ReferenceEquals(team?.PlayerOrderController?.Owner, main),
            MainOnPlayerTeam: main != null && ReferenceEquals(main.Team, team),
            PlayerAttacks: PlayerAttacks);
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

        // A hero of the shop is matched by the body the field holds, before the team is read: his removal carries his shop id, loss or rout. In a campaign the fallen
        // player can take over a free hero (#1366); the engine hands every formation to the AI when the main agent is removed (Mission.cs:3017, :3039-3041), so the
        // removal also says the body was the player's.
        if (TakeHero(agent, out var heroId))
        {
            _registry.TakeRemoval(agent, out _, out _, out var heroKey);
            return new LdRemoval(LdRemovalKind.Hero, heroId, Loss: loss, Key: heroKey, PlayerBody: IsPlayerBody(agent));
        }

        var id = agent.Character?.StringId ?? "";
        _registry.TakeRemoval(agent, out var role, out var felled, out var key);
        var team = agent.Team;
        if (team == null)
            return new LdRemoval(LdRemovalKind.None, "", Key: key);

        // The player's body on his own team, before the side: in a story attack (#1409) it stands on the attacker team, and its loss is his fall, never a wave kill.
        if (team == PlayerSideTeam && IsPlayerBody(agent))
            return loss ? new LdRemoval(LdRemovalKind.Player, id, Key: key, Forced: _lordWatch.IsForced(agent)) : new LdRemoval(LdRemovalKind.None, "", Key: key);

        if (team.Side == BattleSideEnum.Attacker)
        {
            if (loss)
                return new LdRemoval(LdRemovalKind.Attacker, id, role, felled, true, key);

            return role == AttackerRole.Commander ? new LdRemoval(LdRemovalKind.Attacker, id, role, false, false, key) : new LdRemoval(LdRemovalKind.None, "", Key: key);
        }

        if (!loss)
            return new LdRemoval(LdRemovalKind.None, "", Key: key);

        // Every TAOM spawn names its team (Spawn), so Coop's ally team (#1280) holds only the other players' lords: no garrison loss.
        if (team != _mission.DefenderTeam)
            return new LdRemoval(LdRemovalKind.None, "", Key: key);

        return new LdRemoval(LdRemovalKind.Defender, id, Key: key);
    }

    // The engine nulls MainAgent around this callback, so the body the adapter last saw is the player's too.
    private bool IsPlayerBody(Agent agent) => agent == _mission.MainAgent || ReferenceEquals(agent, _knownMain) || agent.Controller == AgentControllerType.Player;

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
        // Another player's lord (#1280, Coop's ally team) takes no poison from this run: his health is his own member's.
        if (hurt.Team != _mission.DefenderTeam)
            return false;
        if (!_registry.TryGetKey(hitter, out attacker))
            return false;

        victim = _registry.Register(hurt, AttackerRole.Roster);
        return true;
    }
}
