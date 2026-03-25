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

    public void KillByOldAge(string heroId)
    {
        var hero = Hero.Find(heroId);
        if (hero != null && hero.IsAlive)
        {
            KillCharacterAction.ApplyByOldAge(hero);
        }
    }
}
