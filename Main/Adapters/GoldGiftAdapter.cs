using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace TAOM.Adapters;

public class GoldGiftAdapter : IGoldGiftAdapter
{
    public void GiveGoldToHero(string heroId, int amount)
    {
        var hero = Hero.FindFirst(h => h.StringId == heroId);
        if (hero == null)
            return;

        GiveGoldAction.ApplyBetweenCharacters(null, hero, amount, disableNotification: true);
    }
}
