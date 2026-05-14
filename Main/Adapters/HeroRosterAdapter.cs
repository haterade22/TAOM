using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

public class HeroRosterAdapter : IHeroRosterAdapter
{
    public IReadOnlyList<HeroRaceInfo> GetAllAliveHeroRaces()
    {
        // Phase 9b #130 P2 — `h.CharacterObject` is a computed property and can be null in
        // transient states. NRE here during OnBeforeSaveEvent would abort the save with no
        // recovery path. Per `adapters.md` rule, use `?.` on computed TaleWorlds properties.
        return Hero.AllAliveHeroes
            .Where(h => h?.CharacterObject != null)
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
        if (hero?.CharacterObject != null)
        {
            hero.CharacterObject.Race = race;
        }
    }
}
