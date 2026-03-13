using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

public class ClanPopulationAdapter : IClanPopulationAdapter
{
    public IReadOnlyList<ClanPopulationInfo> GetMajorFactionClans()
    {
        var results = new List<ClanPopulationInfo>();

        foreach (var clan in Clan.All)
        {
            if (clan.IsBanditFaction || clan.IsMinorFaction || clan.IsEliminated)
                continue;
            if (clan.Kingdom == null)
                continue;
            if (clan == Clan.PlayerClan)
                continue;

            var maleIds = new List<string>();
            var femaleIds = new List<string>();
            int childCount = 0;

            foreach (var hero in clan.Heroes)
            {
                if (!hero.IsAlive) continue;

                if (hero.IsChild)
                {
                    childCount++;
                    continue;
                }

                if (hero.IsFemale)
                    femaleIds.Add(hero.StringId);
                else
                    maleIds.Add(hero.StringId);
            }

            results.Add(new ClanPopulationInfo(
                clan.StringId,
                clan.Culture?.StringId ?? "",
                maleIds,
                femaleIds,
                childCount));
        }

        return results;
    }
}
