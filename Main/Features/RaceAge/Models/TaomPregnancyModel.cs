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

        // The engine only ever passes the FEMALE here (PregnancyCampaignBehavior.DailyTickHero
        // gates on hero.IsFemale), so an immortal FATHER (Sauron, Saruman, a wraith) must be
        // blocked via the spouse's race or his immortal flag never gates conception (#321).
        if (_raceAgeService.IsImmortal(hero.Spouse.CharacterObject.Race))
            return 0f;

        int comesOfAge = _raceAgeService.GetComesOfAge(race);
        int fertilityEnd = _raceAgeService.GetFertilityEndAge(race);

        if (hero.Age < comesOfAge || hero.Age > fertilityEnd)
            return 0f;

        bool playerOrSpouseInvolved = (hero == Hero.MainHero) || (hero.Spouse == Hero.MainHero);
        float fertilityModifier = _raceAgeService.GetFertilityModifier(race);

        // Phase 9b Codex review (2026-05-13): heroAge MUST be float (not int). Vanilla
        // DefaultPregnancyModel.GetDailyChanceOfPregnancyForHero uses Hero.Age (float) directly
        // in the ageFactor formula. Truncating to int rounds fractional age toward zero, so a
        // 44.9-year-old hero would compute as 44 — materially shifting late-window pregnancy
        // chance. Pre-extract code used `hero.Age` directly; passing (int) here was a regression
        // in the extraction commit. Keep heroAge as float to preserve byte-for-byte equivalence.
        float baseChance = ComputeBaseChance(
            heroAge: hero.Age,
            comesOfAge: comesOfAge,
            fertilityEnd: fertilityEnd,
            childCount: hero.Children.Count,
            clanTier: hero.Clan.Tier,
            aliveLords: hero.Clan.AliveLords.Count,
            playerOrSpouseInvolved: playerOrSpouseInvolved,
            raceFertilityModifier: fertilityModifier);

        var result = new ExplainedNumber(baseChance);
        if (hero.GetPerkValue(DefaultPerks.Charm.Virile) || hero.Spouse.GetPerkValue(DefaultPerks.Charm.Virile))
        {
            result.AddFactor(DefaultPerks.Charm.Virile.PrimaryBonus, DefaultPerks.Charm.Virile.Name);
        }

        return result.ResultNumber;
    }

    /// <summary>
    /// Pure-math reimplementation of vanilla DefaultPregnancyModel with race-specific age bounds.
    /// Vanilla hardcodes age 18-45; we replace with the race's comesOfAge..fertilityEnd window.
    ///
    /// Extracted as a public static helper so the 5 branches the Phase 7 audit (#179) flagged can be
    /// unit-tested without the sealed-Hero coupling. Full ADR-007 refactor (introduce IHeroAgeInfo
    /// adapter and move this into IRaceAgeService) is tracked separately as #131.
    /// </summary>
    public static float ComputeBaseChance(
        float heroAge,
        int comesOfAge,
        int fertilityEnd,
        int childCount,
        int clanTier,
        int aliveLords,
        bool playerOrSpouseInvolved,
        float raceFertilityModifier)
    {
        int fertilityWindow = fertilityEnd - comesOfAge;
        float declineRate = fertilityWindow > 0 ? 1.08f / fertilityWindow : 0.04f;
        float ageFactor = 1.2f - (heroAge - comesOfAge) * declineRate;

        int effectiveChildCount = childCount + 1;
        float clanCap = 4 + 4 * clanTier;
        float populationFactor = !playerOrSpouseInvolved
            ? Math.Min(1f, (2f * clanCap - aliveLords) / clanCap)
            : 1f;

        float baseChance = ageFactor / (effectiveChildCount * effectiveChildCount) * 0.12f * populationFactor;
        baseChance *= raceFertilityModifier;
        return baseChance;
    }
}
