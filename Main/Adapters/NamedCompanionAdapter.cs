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

    public bool IsRecruitedOrInParty(string characterId)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
        if (hero == null) return false;
        // #127 P2 — broadened to include captor parties. A companion in a captor party
        // (PartyBelongedToAsPrisoner != null) is semantically "in a party" — skip placement.
        return hero.CompanionOf != null
            || hero.PartyBelongedTo != null
            || hero.PartyBelongedToAsPrisoner != null;
    }

    public bool IsHeroPrisoner(string characterId)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
        if (hero == null) return false;
        // Hero.IsPrisoner covers both settlement-prison and mobile-captor-party prisoners.
        // Belt-and-braces with PartyBelongedToAsPrisoner per the #127 audit description.
        return hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != null;
    }

    public bool IsHeroFugitive(string characterId)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
        if (hero == null) return false;
        return hero.HeroState == Hero.CharacterStates.Fugitive;
    }

    public bool EnsureHomeSettlement(string characterId, string settlementId)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
        if (hero == null || hero.BornSettlement != null) return false;

        var settlement = Settlement.Find(settlementId);
        if (settlement == null) return false;

        // Hero.UpdateHomeSettlement falls all the way through to _bornSettlement for a companion
        // with no clan, no CompanionOf and no governor relatives, so this is the field that decides
        // whether MapFaction resolves. The setter clears the cached _homeSettlement for us. This is
        // what HeroCreator.InitializeHeroFromSettings does for every runtime-created wanderer —
        // XML heroes are the only ones that miss it.
        hero.BornSettlement = settlement;
        return true;
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
