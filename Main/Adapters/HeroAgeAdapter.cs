using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace TAOM.Adapters;

public class HeroAgeAdapter : IHeroAgeAdapter
{
    public IEnumerable<HeroAgeInfo> GetAllAliveHeroAges()
    {
        return Hero.AllAliveHeroes
            .Where(h => !h.IsChild)
            .Select(h => new HeroAgeInfo(h.StringId, h.CharacterObject.Race, h.Age));
    }

    public bool KillByOldAge(string heroId)
    {
        var hero = Hero.Find(heroId);
        if (hero == null || !hero.IsAlive) return false;

        KillCharacterAction.ApplyByOldAge(hero);

        // ApplyByOldAge is not guaranteed to kill: it early-returns when the hero can't die
        // (life/death cycle disabled), marks-and-defers while the hero is in a MapEvent/SiegeEvent,
        // and refuses the player character outright. Re-read IsAlive so the caller only announces
        // deaths that actually landed.
        return !hero.IsAlive;
    }
}
