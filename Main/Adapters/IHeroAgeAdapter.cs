using System.Collections.Generic;

namespace TAOM.Adapters;

public readonly struct HeroAgeInfo
{
    public string HeroId { get; }
    public int Race { get; }
    public float Age { get; }

    public HeroAgeInfo(string heroId, int race, float age)
    {
        HeroId = heroId;
        Race = race;
        Age = age;
    }
}

public interface IHeroAgeAdapter
{
    IReadOnlyList<HeroAgeInfo> GetAllAliveHeroAges();
    void KillByOldAge(string heroId);
}
