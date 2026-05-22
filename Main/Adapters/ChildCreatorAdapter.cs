using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

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

        // HeroCreator.CreateChild inherits the template's sex. When the service
        // fell back to the opposite-sex pool (zero-male clan), enforce the requested sex.
        // Deep-review 2026-05-22: prior implementation reflected on
        // BasicCharacterObject.<IsFemale>k__BackingField — silent no-op, because
        // CharacterObject.IsFemale is an override that unconditionally returns
        // HeroObject.IsFemale for heroes. Hero.IsFemale has a public setter; use it.
        if (hero.IsFemale != isFemale)
            hero.IsFemale = isFemale;

        hero.UpdateHomeSettlement();
        hero.HeroDeveloper.InitializeHeroDeveloper();

        AssignEquipment(hero);
    }

    private static void AssignEquipment(Hero hero)
    {
        // v1.4.3 renamed GetEquipmentRostersForInitialChildrenGeneration (MBList<MBEquipmentRoster>)
        // to GetEquipmentForInitialChildrenGeneration (single Equipment). Gender + culture
        // filtering moved inside the model; the engine returns the civilian-shape Equipment
        // and the caller derives battle from it via FillFrom. Mirrors vanilla 1.4.5
        // InitialChildGenerationCampaignBehavior.
        Equipment civilianEquipment = Campaign.Current.Models.EquipmentSelectionModel
            .GetEquipmentForInitialChildrenGeneration(hero);
        if (civilianEquipment == null) return;

        EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, civilianEquipment);

        var battleEquipment = new Equipment(Equipment.EquipmentType.Battle);
        battleEquipment.FillFrom(civilianEquipment, useSourceEquipmentType: false);
        EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, battleEquipment);
    }
}
