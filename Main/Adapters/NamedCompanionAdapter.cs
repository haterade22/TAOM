using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

public class NamedCompanionAdapter : INamedCompanionAdapter
{
    public bool HeroExists(string characterId)
    {
        return Hero.AllAliveHeroes.Any(h => h.StringId == characterId)
            || Hero.DeadOrDisabledHeroes.Any(h => h.StringId == characterId);
    }

    public bool IsHeroAlive(string characterId)
    {
        return Hero.AllAliveHeroes.Any(h => h.StringId == characterId);
    }

    public bool IsPlacedInSettlement(string characterId)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
        return hero?.StayingInSettlement != null || hero?.CurrentSettlement != null;
    }

    public void PlaceInSettlement(string characterId, string settlementId)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
        var settlement = Settlement.Find(settlementId);
        if (hero != null && settlement != null)
        {
            hero.ChangeState(Hero.CharacterStates.Active);
            EnterSettlementAction.ApplyForCharacterOnly(hero, settlement);
        }
    }

    public void MarkAsMet(string characterId)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
        hero?.SetHasMet();
    }
}
