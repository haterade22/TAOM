using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

public class ClanStartupAdapter : IClanStartupAdapter
{
    public IReadOnlyList<ClanStartupInfo> GetEligibleClans()
    {
        var results = new List<ClanStartupInfo>();

        foreach (var clan in Clan.All)
        {
            if (clan.IsBanditFaction || clan.IsMinorFaction || clan.IsEliminated)
                continue;
            if (clan.Kingdom == null)
                continue;
            if (clan == Clan.PlayerClan)
                continue;
            if (clan.Leader == null)
                continue;

            results.Add(new ClanStartupInfo(
                clan.StringId,
                clan.Culture?.StringId ?? ""));
        }

        return results;
    }

    public void AddInfluence(string clanId, float amount)
    {
        var clan = Clan.All.FirstOrDefault(c => c.StringId == clanId);
        if (clan != null)
            clan.Influence += amount;
    }
}
