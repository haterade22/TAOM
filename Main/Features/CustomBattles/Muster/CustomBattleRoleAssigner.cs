using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TAOM.Adapters;

namespace TAOM.Features.CustomBattles.Muster;

/// <summary>
/// The muster's role hand-out and census for <see cref="CustomBattleRolesBehavior"/>. Boundary code (raw Mission,
/// Team and Agent); game-tested per ADR-008.
/// </summary>
internal static class CustomBattleRoleAssigner
{
    internal static void Hand(Mission m, IReadOnlyList<CustomBattleMuster.Role> plan, List<Agent> hornBlowers)
    {
        hornBlowers.Clear();
        var bannerLogic = m.GetMissionBehavior<BannerBearerLogic>();
        foreach (var r in plan)
        {
            var team = r.Side == m.PlayerTeam?.Side ? m.PlayerTeam : m.PlayerEnemyTeam;
            if (team == null) continue;
            var men = team.ActiveAgents.Where(a => a.Character == r.Unit).ToList();
            var formation = men.Where(a => a.Formation != null).GroupBy(a => a.Formation)
                .OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
            var parts = new List<string>();
            if (r.Captain != null) parts.Add(Captain(team, r.Captain, formation));
            if (r.Banner) parts.Add(Banner(bannerLogic, team, formation));
            if (r.Horn)
            {
                var bearers = formation != null && bannerLogic != null
                    ? bannerLogic.GetFormationBannerBearers(formation)
                    : new List<Agent>();
                var horn = men.FirstOrDefault(a => !bearers.Contains(a) && a != formation?.Captain);
                if (horn != null) { hornBlowers.Add(horn); parts.Add($"horn blower {horn.Name}"); }
                else parts.Add("horn blower: no soldier left to mark");
            }
            CustomBattleMuster.Log($"roles {r.Side} {r.Unit.StringId} ({men.Count} on the field): {string.Join(", ", parts)}");
        }
    }

    private static string Captain(Team team, BasicCharacterObject captain, Formation? formation)
    {
        var cap = team.ActiveAgents.FirstOrDefault(a => a.Character == captain && a.Formation?.Captain != a && !a.IsMainAgent);
        if (cap == null) return "captain NOT FOUND";
        if (formation == null) return "captain found, but the unit has no formation";
        if (cap.Formation != formation) cap.Formation = formation;
        formation.Captain = cap;
        return $"captain {cap.Name} leads {formation.FormationIndex}";
    }

    private static string Banner(BannerBearerLogic? bannerLogic, Team team, Formation? formation)
    {
        if (bannerLogic == null) return "banner: no BannerBearerLogic in this mission";
        var item = BannerFor(team);
        if (item == null) return "banner: no banner item";
        if (formation == null) return "banner: the unit has no formation";
        bannerLogic.SetFormationBanner(formation, item);
        return $"banner {item.StringId} on {formation.FormationIndex}, bearers now {bannerLogic.GetFormationBannerBearers(formation).Count}";
    }

    private static ItemObject? BannerFor(Team team)
    {
        var culture = CharacterCulture.Of(team.Leader?.Character) ?? CharacterCulture.Of(team.ActiveAgents.FirstOrDefault()?.Character);
        var banners = MBObjectManager.Instance.GetObjectTypeList<ItemObject>().Where(BannerBearerLogic.IsBannerItem).ToList();
        return banners.FirstOrDefault(i => i.Culture == culture) ?? banners.FirstOrDefault();
    }

    internal static void Census(Mission m)
    {
        foreach (var team in new[] { m.PlayerTeam, m.PlayerEnemyTeam })
        {
            if (team == null) continue;
            var rows = team.ActiveAgents.Where(a => a.IsHuman).GroupBy(a => a.Character?.StringId ?? "?")
                .OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}");
            CustomBattleMuster.Log($"on the field, {team.Side}: {team.ActiveAgents.Count(a => a.IsHuman)} men: {string.Join(", ", rows)}");
        }
    }
}
