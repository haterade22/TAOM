using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace TAOM.Adapters;

public class HeroAgeAdapter : IHeroAgeAdapter
{
    public IReadOnlyList<HeroAgeInfo> GetAllAliveHeroAges()
    {
        return Hero.AllAliveHeroes
            .Where(h => !h.IsChild)
            .Select(h => new HeroAgeInfo(h.StringId, h.CharacterObject.Race, h.Age))
            .ToList();
    }

    public void KillByOldAge(string heroId)
    {
        var hero = Hero.FindFirst(h => h.StringId == heroId);
        if (hero != null && hero.IsAlive)
        {
            KillCharacterAction.ApplyByOldAge(hero);
        }
    }
}
