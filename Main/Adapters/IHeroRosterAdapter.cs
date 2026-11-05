using System.Collections.Generic;

namespace TAOM.Adapters;

public readonly struct HeroRaceInfo
{
    public string StringId { get; }
    public int Race { get; }

    public HeroRaceInfo(string stringId, int race)
    {
        StringId = stringId;
        Race = race;
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
