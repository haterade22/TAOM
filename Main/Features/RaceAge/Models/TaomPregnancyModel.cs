using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TAOM.Features.RaceAge.Models;

public class TaomPregnancyModel : DefaultPregnancyModel
{
    private readonly IRaceAgeService _raceAgeService;

    public TaomPregnancyModel(IRaceAgeService raceAgeService)
    {
        _raceAgeService = raceAgeService;
    }

    public override float GetDailyChanceOfPregnancyForHero(Hero hero)
    {
        var race = hero.CharacterObject.Race;

        if (_raceAgeService.IsImmortal(race))
            return 0f;

        var baseChance = base.GetDailyChanceOfPregnancyForHero(hero);
        if (baseChance <= 0f)
            return 0f;

        if (hero.Age > _raceAgeService.GetFertilityEndAge(race))
            return 0f;

        return baseChance * _raceAgeService.GetFertilityModifier(race);
    }
}
