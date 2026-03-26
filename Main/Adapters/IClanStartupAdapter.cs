using System.Collections.Generic;

namespace TAOM.Adapters;

public readonly struct ClanStartupInfo
{
    public string ClanId { get; }
    public string CultureId { get; }

    public ClanStartupInfo(string clanId, string cultureId)
    {
        ClanId = clanId;
        CultureId = cultureId;
    }
}

public interface IClanStartupAdapter
{
    IReadOnlyList<ClanStartupInfo> GetEligibleClans();
    void AddInfluence(string clanId, float amount);
}
