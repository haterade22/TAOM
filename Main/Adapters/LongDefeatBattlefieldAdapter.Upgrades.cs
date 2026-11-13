using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Upgrading the defenders between waves (#1161). Each soldier is replaced the way the remount replaces the lord (RemountPlayer): his health share is read, he is faded (FadeOut hands removal to
/// native, Agent.cs:4304-4310, and native reports him Routed, which LongDefeatMissionLogic.IsLoss and the morale logic, AgentMoraleInteractionLogic.cs:42, both read as no loss), and the new
/// troop spawns at his place and facing in his formation: a given position skips the formation layout (the Spawn recipe's note, Mission.cs:4227), and GetAgentBuildDataToSpawnTroop puts him in
/// the formation of the class it is given (Mission.cs:4461-4500).
/// </summary>
internal sealed partial class LongDefeatBattlefieldAdapter
{
    public IReadOnlyDictionary<string, int> StandingDefendersByTroop()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var agent in Upgradable())
        {
            var id = agent.Character.StringId;
            counts[id] = (counts.TryGetValue(id, out var had) ? had : 0) + 1;
        }

        return counts;
    }

    public LdUpgradeReport ReplaceDefenders(string fromTroop, string toTroop, int count, float healthFactor)
    {
        var troop = MBObjectManager.Instance.GetObject<BasicCharacterObject>(toTroop);
        var team = _mission.DefenderTeam;
        if (troop == null || team == null)
            return new LdUpgradeReport(0, 0, 0, troop == null ? $"no troop '{toTroop}'" : "no defender team");

        // Taken into a list first: each fade and spawn changes the team's agent list.
        var old = Upgradable().Where(a => string.Equals(a.Character.StringId, fromTroop, StringComparison.OrdinalIgnoreCase)).ToList();
        int replaced = 0, readBack = 0;
        foreach (var agent in old.Take(Math.Max(0, count)))
        {
            var share = LdRemount.ShareOf(agent.Health, agent.HealthLimit);
            var cls = agent.Formation?.FormationIndex ?? troop.DefaultFormationClass;
            var at = agent.Position;
            var look = agent.LookDirection.AsVec2;
            var facing = look.Length > 1e-4f ? look.Normalized() : new Vec2(0f, 1f);
            agent.FadeOut(true, false);

            var fresh = Spawn(troop, team, true, cls, true, 1, 0, false, place: new LdPlace(at.x, at.y, at.z, facing.x, facing.y));
            ScaleHealth(fresh, healthFactor);
            if (fresh.HealthLimit > 0f)
                fresh.Health = Math.Max(1f, fresh.HealthLimit * share);
            replaced++;
            if (fresh.IsActive() && string.Equals(fresh.Character?.StringId, toTroop, StringComparison.OrdinalIgnoreCase))
                readBack++;
        }

        return new LdUpgradeReport(old.Count, replaced, readBack, null);
    }

    // On foot, so a fade leaves no riderless horse and the new soldier (who spawns with no horse) is no downgrade; never the player or the lord's body; never a hero; never a man working an
    // engine (a fade would leave it mid-use); never a tower post archer, whom the posts hold by agent.
    private IEnumerable<Agent> Upgradable()
    {
        var team = _mission.DefenderTeam;
        if (team == null)
            return Enumerable.Empty<Agent>();

        return team.ActiveAgents.Where(a => a.IsHuman && a.IsActive() && a.Character != null && !a.Character.IsHero && !a.HasMount && !a.IsUsingGameObject
            && a != _mission.MainAgent && !ReferenceEquals(a, _lordWatch.Lord) && a.Controller != AgentControllerType.Player && a.Formation?.FormationIndex != PostsClass).ToList();
    }
}
