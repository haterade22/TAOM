using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

public class HeroRosterAdapter : IHeroRosterAdapter
{
    public IReadOnlyList<HeroRaceInfo> GetAllAliveHeroRaces()
    {
        return Hero.AllAliveHeroes
            .Select(h => new HeroRaceInfo(h.StringId, h.CharacterObject.Race))
            .ToList();
    }

    public int GetHeroRace(string heroStringId)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == heroStringId);
        return hero?.CharacterObject?.Race ?? 0;
    }

    public void SetHeroRace(string heroStringId, int race)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == heroStringId);
        if (hero != null)
        {
            hero.CharacterObject.Race = race;
        }
    }
}
