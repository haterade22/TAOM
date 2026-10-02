using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;

namespace TAOM.Features.TournamentRewards;

/// <summary>
/// Tournament rewards (docs/features/tournament-rewards.md): the bet cap, the winner's renown and influence, and
/// the skill the player chose at Join with its XP. Holds two pieces of transient campaign state, both written and
/// read within one tournament: the hero count of the tournament that just finished (the engine's reward and
/// winner-panel calls ask for it by town only) and the player's skill choice per town.
/// <see cref="ResetForNewSession"/> clears both at each session launch.
/// </summary>
public sealed class TournamentRewardsService
{
    private readonly ITournamentRewardsSettingsProvider _settings;
    private readonly ITournamentRewardsConfigProvider _config;
    private readonly IHeroSkillXpAdapter _xp;
    private readonly Dictionary<string, string> _skillByTown = new(StringComparer.Ordinal);

    private string? _finishedTownId;
    private int _finishedHeroCount;

    public TournamentRewardsService(
        ITournamentRewardsSettingsProvider settings,
        ITournamentRewardsConfigProvider config,
        IHeroSkillXpAdapter xp)
    {
        _settings = settings;
        _config = config;
        _xp = xp;
    }

    public int MaximumBet(int vanillaResult) =>
        TournamentRewardRules.MaximumBet(vanillaResult, _settings.MaxBetPerRound);

    /// <summary>Called as a tournament finishes, before the engine asks for that tournament's rewards.</summary>
    public void NoteTournamentFinished(string? townId, int heroCount)
    {
        _finishedTownId = townId;
        _finishedHeroCount = heroCount;
    }

    public int RenownReward(int vanillaRenown, string? townId, string? winnerCultureId) =>
        TournamentRewardRules.Renown(vanillaRenown, HeroCountFor(townId),
            _config.GetCatalog().For(winnerCultureId).Renown, _settings.RenownMultiplier);

    /// <summary>Whole points: the engine's GetInfluenceReward returns an int (v1.5.3).</summary>
    public int InfluenceReward(int vanillaInfluence, string? townId, string? winnerCultureId,
        string? winnerKingdomId, string? townKingdomId)
    {
        var ownKingdomTown = !string.IsNullOrEmpty(winnerKingdomId)
                             && string.Equals(winnerKingdomId, townKingdomId, StringComparison.Ordinal);
        var influence = TournamentRewardRules.Influence(vanillaInfluence, HeroCountFor(townId), ownKingdomTown,
            _config.GetCatalog().For(winnerCultureId).Influence, _settings.InfluenceMultiplier);
        return (int)Math.Round(influence, MidpointRounding.AwayFromZero);
    }

    /// <summary>The skill the player chose at Join for this town's tournament; an unknown skill is ignored.</summary>
    public void RememberSkillChoice(string townId, string skillId)
    {
        if (!string.IsNullOrEmpty(townId) && TournamentRewardRules.CombatSkillIds.Contains(skillId))
            _skillByTown[townId] = skillId;
    }

    public string? ChosenSkill(string? townId) =>
        townId != null && _skillByTown.TryGetValue(townId, out var skill) ? skill : null;

    /// <summary>
    /// Trains the chosen skill once the player's tournament ends, and forgets the choice. Returns the XP
    /// awarded, 0 when there was no choice, nothing to award, or the hero could not take it.
    /// </summary>
    public int AwardPlayerSkillXp(string? townId, int roundsWon, bool wonTournament, string? playerHeroId, string? playerCultureId)
    {
        var skill = ChosenSkill(townId);
        if (skill == null)
            return 0;
        _skillByTown.Remove(townId!);
        if (string.IsNullOrEmpty(playerHeroId))
            return 0;
        var xp = TournamentRewardRules.SkillXp(roundsWon, wonTournament, _config.GetCatalog().For(playerCultureId).SkillXp);
        if (xp <= 0)
            return 0;
        return _xp.AddSkillXp(playerHeroId!, skill, xp) ? xp : 0;
    }

    public void ResetForNewSession()
    {
        _skillByTown.Clear();
        _finishedTownId = null;
        _finishedHeroCount = 0;
    }

    private int HeroCountFor(string? townId) =>
        townId != null && string.Equals(townId, _finishedTownId, StringComparison.Ordinal) ? _finishedHeroCount : 0;
}
