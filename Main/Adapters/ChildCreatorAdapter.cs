using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Adapters;

public class ChildCreatorAdapter : IChildCreatorAdapter
{
    public void CreateChild(string templateHeroId, string clanId, bool isFemale, int age)
    {
        var templateHero = Hero.FindFirst(h => h.StringId == templateHeroId);
        if (templateHero == null) return;

        var clan = Clan.FindFirst(c => c.StringId == clanId);
        if (clan == null) return;

        var hero = HeroCreator.CreateChild(
            templateHero.CharacterObject,
            clan.HomeSettlement,
            clan,
            age);

        hero.UpdateHomeSettlement();
        hero.HeroDeveloper.InitializeHeroDeveloper();

        AssignEquipment(hero);
    }

    private static void AssignEquipment(Hero hero)
    {
        var rosters = Campaign.Current.Models.EquipmentSelectionModel
            .GetEquipmentRostersForInitialChildrenGeneration(hero);

        if (rosters == null || rosters.Count == 0) return;

        var roster = rosters.GetRandomElementInefficiently();
        if (roster == null) return;

        Equipment civilianEquipment = roster.GetRandomCivilianEquipment();
        if (civilianEquipment == null) return;

        EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, civilianEquipment);

        var battleEquipment = new Equipment(Equipment.EquipmentType.Battle);
        battleEquipment.FillFrom(civilianEquipment, useSourceEquipmentType: false);
        EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, battleEquipment);
    }
}
