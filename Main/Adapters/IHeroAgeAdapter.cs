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
    IEnumerable<HeroAgeInfo> GetAllAliveHeroAges();

    /// <summary>
    /// Applies an old-age death. Returns true only if the hero is actually dead afterwards —
    /// the engine's KillCharacterAction silently no-ops while a hero is in a MapEvent/SiegeEvent
    /// (death is deferred until the battle resolves) and for the player character. Callers must
    /// not announce a death this returns false for.
    /// </summary>
    bool KillByOldAge(string heroId);
}
