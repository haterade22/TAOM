using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace TAOM.Adapters;

public class HeroCaptivityAdapter : IHeroCaptivityAdapter
{
    public bool MakeFugitive(string heroStringId)
    {
        if (string.IsNullOrEmpty(heroStringId))
            return false;

        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == heroStringId);
        if (hero == null)
            return false;

        // showNotification stays false: vanilla's notification is a generic "became a fugitive"
        // line, and TAOM emits its own localized string naming the escape instead.
        MakeHeroFugitiveAction.Apply(hero);
        return true;
    }
}
