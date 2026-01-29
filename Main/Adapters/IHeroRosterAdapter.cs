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
    IReadOnlyList<HeroRaceInfo> GetAllAliveHeroRaces();
    void SetHeroRace(string heroStringId, int race);
}
