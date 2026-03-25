using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

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

        if (hero.Spouse == null)
            return 0f;

        int comesOfAge = _raceAgeService.GetComesOfAge(race);
        int fertilityEnd = _raceAgeService.GetFertilityEndAge(race);

        if (hero.Age < comesOfAge || hero.Age > fertilityEnd)
            return 0f;

        // Reimplementation of vanilla DefaultPregnancyModel with race-specific age bounds.
        // Vanilla hardcodes age 18-45; we replace with race-specific comesOfAge and fertilityEnd.
        int fertilityWindow = fertilityEnd - comesOfAge;
        float declineRate = fertilityWindow > 0 ? 1.08f / fertilityWindow : 0.04f;
        float ageFactor = 1.2f - (hero.Age - comesOfAge) * declineRate;

        int childCount = hero.Children.Count + 1;
        float clanCap = 4 + 4 * hero.Clan.Tier;
        int aliveLords = hero.Clan.AliveLords.Count;
        float populationFactor = (hero != Hero.MainHero && hero.Spouse != Hero.MainHero)
            ? Math.Min(1f, (2f * clanCap - aliveLords) / clanCap)
            : 1f;

        float baseChance = ageFactor / (childCount * childCount) * 0.12f * populationFactor;

        // Apply race fertility modifier
        baseChance *= _raceAgeService.GetFertilityModifier(race);

        var result = new ExplainedNumber(baseChance);
        if (hero.GetPerkValue(DefaultPerks.Charm.Virile) || hero.Spouse.GetPerkValue(DefaultPerks.Charm.Virile))
        {
            result.AddFactor(DefaultPerks.Charm.Virile.PrimaryBonus, DefaultPerks.Charm.Virile.Name);
        }

        return result.ResultNumber;
    }
}
