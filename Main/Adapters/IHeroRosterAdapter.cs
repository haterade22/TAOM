using System.Collections.Generic;

namespace TAOM.Adapters;

public readonly struct HeroRaceInfo
{
    public string StringId { get; }
    public int Race { get; }
    // Hero.IsAlive (#1011): a disabled hero counts as alive. The race-persist check lines count the dead.
    public bool IsAlive { get; }

    public HeroRaceInfo(string stringId, int race, bool isAlive = true)
    {
        StringId = stringId;
        Race = race;
        IsAlive = isAlive;
    }
}

public interface IHeroRosterAdapter
{
    /// <summary>Every hero character, alive, dead, disabled or unregistered (#1011): the engine
    /// resets the race of every non-original character on load, dead ones included.</summary>
    IReadOnlyList<HeroRaceInfo> GetAllHeroRaces();
    int GetHeroRace(string heroStringId);
    void SetHeroRace(string heroStringId, int race);
}
