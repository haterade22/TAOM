using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.RaceAge.Models;

public class TaomPregnancyModel : DefaultPregnancyModel
{
    private readonly IRaceAgeService _raceAgeService;
    private readonly IPlayerControlQuery _players;

    // The query is resolved here, not as a constructor parameter: that would need an edit to SubModule.cs, where this
    // model is built.
    public TaomPregnancyModel(IRaceAgeService raceAgeService)
        : this(raceAgeService, IoC.Resolve<IPlayerControlQuery>())
    {
    }

    internal TaomPregnancyModel(IRaceAgeService raceAgeService, IPlayerControlQuery players)
    {
        _raceAgeService = raceAgeService;
        _players = players;
    }

    public override float GetDailyChanceOfPregnancyForHero(Hero hero)
    {
        if (hero.Spouse == null)
            return 0f;

        var race = hero.CharacterObject.Race;

        // Before any Clan read: the engine lets a clanless hero through (PregnancyCampaignBehavior.DailyTickHero).
        if (!_raceAgeService.CanConceive(race, hero.Spouse.CharacterObject.Race, hero.Age))
            return 0f;

        bool playerOrSpouseInvolved = IsPlayerMarriage(hero, hero.Spouse, _players);

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

    /// <summary>
    /// Vanilla's "the player or the player's spouse" test (#1141). Pregnancy ticks run on the co-op server, where
    /// <c>Hero.MainHero</c> is the server's own hero; this override never calls the base method, so Coop's prefix
    /// on it (which asks <c>IsPlayerHero</c>) never runs. Without a session the query is the
    /// <c>Hero.MainHero</c> comparison. <see cref="PlayerControl.Unknown"/> is not a player: an unreadable registry
    /// would otherwise lift the clan brake and drop the race bonus for every AI clan.
    /// </summary>
    internal static bool IsPlayerMarriage(Hero hero, Hero? spouse, IPlayerControlQuery players)
        => players.OfHero(hero) == PlayerControl.Player || players.OfHero(spouse) == PlayerControl.Player;
}
