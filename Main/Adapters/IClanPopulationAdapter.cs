using System.Collections.Generic;

namespace TAOM.Adapters;

public readonly struct ClanPopulationInfo
{
    public string ClanId { get; }
    public string CultureId { get; }
    public IReadOnlyList<string> AdultMaleHeroIds { get; }
    public IReadOnlyList<string> AdultFemaleHeroIds { get; }
    public int ExistingChildCount { get; }

    public ClanPopulationInfo(
        string clanId,
        string cultureId,
        IReadOnlyList<string> adultMaleHeroIds,
        IReadOnlyList<string> adultFemaleHeroIds,
        int existingChildCount)
    {
        ClanId = clanId;
        CultureId = cultureId;
        AdultMaleHeroIds = adultMaleHeroIds;
        AdultFemaleHeroIds = adultFemaleHeroIds;
        ExistingChildCount = existingChildCount;
    }
}

public interface IClanPopulationAdapter
{
    IReadOnlyList<ClanPopulationInfo> GetMajorFactionClans();
}
