using System.Collections.Generic;

namespace TAOM.Adapters;

public readonly struct LordHeroInfo
{
    public string HeroId { get; }
    public string CultureId { get; }
    public bool IsPlayerClan { get; }

    public LordHeroInfo(string heroId, string cultureId, bool isPlayerClan)
    {
        HeroId = heroId;
        CultureId = cultureId;
        IsPlayerClan = isPlayerClan;
    }
}

public interface IStartupHeroAdapter
{
    IReadOnlyList<LordHeroInfo> GetAliveLordHeroes();
}
