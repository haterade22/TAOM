using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Adapters;

public class TroopRosterQueryAdapter : ITroopRosterQueryAdapter
{
    private static TroopRoster MainRoster => MobileParty.MainParty?.MemberRoster;

    public bool HasMainParty => MainRoster != null;

    public int GetMainPartyHealthyCount() => MainRoster?.TotalHealthyCount ?? 0;

    public IReadOnlyDictionary<string, int> GetRosterSnapshot()
    {
        var snapshot = new Dictionary<string, int>();
        var roster = MainRoster;
        if (roster == null)
            return snapshot;

        foreach (TroopRosterElement element in roster.GetTroopRoster())
        {
            if (element.Character == null || element.Character.IsHero || element.Number <= 0)
                continue;

            snapshot[element.Character.StringId] = element.Number;
        }

        return snapshot;
    }

    public int GetTroopCount(string troopId)
    {
        var roster = MainRoster;
        var troop = ResolveTroop(troopId);
        if (roster == null || troop == null)
            return 0;

        return roster.GetTroopCount(troop);
    }

    public bool RemoveOneFromRoster(string troopId)
    {
        var roster = MainRoster;
        var troop = ResolveTroop(troopId);
        if (roster == null || troop == null || roster.GetTroopCount(troop) <= 0)
            return false;

        roster.AddToCounts(troop, -1, false, 0, 0, true, -1);
        return true;
    }

    public TroopInfo GetTroopInfo(string troopId)
    {
        var troop = ResolveTroop(troopId);
        if (troop == null)
            return TroopInfo.Missing;

        return new TroopInfo(
            troop.StringId,
            troop.Name?.ToString(),
            troop.IsHero,
            troop.Occupation == Occupation.PrisonGuard,
            troop.Race,
            troop.Level);
    }

    public IReadOnlyList<string> GetUpgradeTargetIds(string troopId)
    {
        var troop = ResolveTroop(troopId);
        if (troop?.UpgradeTargets == null)
            return System.Array.Empty<string>();

        var ids = new List<string>(troop.UpgradeTargets.Length);
        foreach (var target in troop.UpgradeTargets)
        {
            // Heroes are excluded (donor parity): a hero sitting in the member roster has a count
            // above zero, so the orphan-merit walk would happily park a dead troop's merit under a
            // hero id — where CanPromote rejects it forever and the consolidation pass never
            // reclaims it, because the count check says it is still present. A silent merit sink.
            if (target != null && !target.IsHero)
                ids.Add(target.StringId);
        }
        return ids;
    }

    public IReadOnlyDictionary<string, int> GetTroopSkillValues(string troopId)
    {
        var values = new Dictionary<string, int>();
        var troop = ResolveTroop(troopId);
        if (troop == null)
            return values;

        foreach (SkillObject skill in Skills.All)
        {
            if (skill == null)
                continue;
            values[skill.StringId] = troop.GetSkillValue(skill);
        }
        return values;
    }

    private static CharacterObject ResolveTroop(string troopId) =>
        string.IsNullOrEmpty(troopId) ? null : MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
}
