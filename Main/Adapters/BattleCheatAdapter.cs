using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CheatMenu.Battle;
using TAOM.Features.DevConsole.Cheats;

namespace TAOM.Adapters;

/// <summary>
/// The battle cheats' engine calls, copied from vanilla's own mission cheats (v1.5.6 decompile <c>Mission.MakeEnemiesFleeCheat</c>,
/// <c>mission.flee_enemies</c>, and <c>Mission.KillAllEnemies</c>, <c>mission.kill_all_enemies</c>). Lists are copied before
/// anything is killed or spawned. Holds no engine object.
/// </summary>
public sealed class BattleCheatAdapter : IBattleCheatAdapter
{
    public BattleCheatState ReadState()
    {
        var mission = Mission.Current;
        if (mission == null) return new BattleCheatState(MissionRunning: false, Ending: false, HasPlayerTeam: false);
        return new BattleCheatState(
            MissionRunning: true,
            Ending: mission.MissionEnded || mission.RetreatSide != BattleSideEnum.None,
            HasPlayerTeam: mission.PlayerTeam != null);
    }

    public int HealMainAgent()
    {
        var main = Mission.Current?.MainAgent;
        if (main == null || !main.IsActive()) return 0;
        return Heal(main) + HealMount(main);
    }

    public int HealPlayerSide()
    {
        var mission = Mission.Current;
        if (mission == null || mission.PlayerTeam == null) return 0;
        var healed = 0;
        foreach (var agent in mission.Agents.ToList())
        {
            if (!IsSoldier(agent) || !agent.Team.IsPlayerAlly) continue;
            healed += Heal(agent) + HealMount(agent);
        }
        return healed;
    }

    public int RoutEnemies()
    {
        var mission = Mission.Current;
        var player = mission?.PlayerTeam;
        if (mission == null || player == null) return 0;
        var routed = 0;
        foreach (var agent in mission.Agents.Where(a => a.IsHuman && a.IsActive() && a.Team != null && a.Team.IsEnemyOf(player)).ToList())
        {
            var ai = agent.CommonAIComponent;
            if (ai == null) continue;
            ai.Panic();
            routed++;
        }
        return routed;
    }

    public int KillEnemies()
    {
        var mission = Mission.Current;
        if (mission == null || mission.PlayerTeam == null) return 0;
        var killed = 0;
        foreach (var team in mission.Teams.ToList())
        {
            if (!team.IsPlayerEnemy) continue;
            foreach (var agent in team.ActiveAgents.ToList())
            {
                if (!agent.IsAIControlled) continue;
                mission.KillAgentCheat(agent);
                killed++;
            }
        }
        return killed;
    }

    public IReadOnlyList<BattleTroop> SideTroops(bool playerSide)
    {
        var mission = Mission.Current;
        if (mission == null || mission.PlayerTeam == null) return Array.Empty<BattleTroop>();
        var troops = new List<BattleTroop>();
        foreach (var agent in mission.Agents.ToList())
        {
            if (!IsSoldier(agent) || agent.Character == null) continue;
            if (playerSide ? !agent.Team.IsPlayerAlly : !agent.Team.IsPlayerEnemy) continue;
            troops.Add(new BattleTroop(agent.Character.StringId, agent.IsHero));
        }
        return troops;
    }

    public int SpawnTroops(string troopId, int count, bool playerSide, int ringStart) =>
        MissionTroopSpawner.Spawn(troopId, count, playerSide, ringStart).Spawned;

    // An active agent with a team that is not a mount: a soldier, a hero or a creature.
    private static bool IsSoldier(Agent agent) => agent != null && agent.IsActive() && !agent.IsMount && agent.Team != null;

    private static int Heal(Agent agent)
    {
        agent.Health = agent.HealthLimit;
        return 1;
    }

    private static int HealMount(Agent rider)
    {
        var mount = rider.MountAgent;
        if (mount == null || !mount.IsActive()) return 0;
        return Heal(mount);
    }
}
