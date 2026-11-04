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
        if (hero.Spouse == null)
            return 0f;

        var race = hero.CharacterObject.Race;

        // Before any Clan read: the engine lets a clanless hero through (PregnancyCampaignBehavior.DailyTickHero).
        if (!_raceAgeService.CanConceive(race, hero.Spouse.CharacterObject.Race, hero.Age))
            return 0f;

        bool playerOrSpouseInvolved = (hero == Hero.MainHero) || (hero.Spouse == Hero.MainHero);

        // Phase 9b Codex review (2026-05-13): heroAge MUST be float (not int). Vanilla
        // DefaultPregnancyModel.GetDailyChanceOfPregnancyForHero uses Hero.Age (float) directly
        // in the ageFactor formula. Truncating to int rounds fractional age toward zero, so a
        // 44.9-year-old hero would compute as 44 — materially shifting late-window pregnancy
        // chance. Pre-extract code used `hero.Age` directly; passing (int) here was a regression
        // in the extraction commit. Keep heroAge as float to preserve byte-for-byte equivalence.
        float baseChance = _raceAgeService.GetDailyPregnancyBaseChance(
            raceId: race,
            heroAge: hero.Age,
            childCount: hero.Children.Count,
            clanTier: hero.Clan.Tier,
            aliveLords: hero.Clan.AliveLords.Count,
            playerOrSpouseInvolved: playerOrSpouseInvolved);

        var result = new ExplainedNumber(baseChance);
        if (hero.GetPerkValue(DefaultPerks.Charm.Virile) || hero.Spouse.GetPerkValue(DefaultPerks.Charm.Virile))
        {
            result.AddFactor(DefaultPerks.Charm.Virile.PrimaryBonus, DefaultPerks.Charm.Virile.Name);
        }

        return result.ResultNumber;
    }
}
