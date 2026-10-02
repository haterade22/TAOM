using System;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.Arena;

namespace TAOM.Features.TournamentRewards;

/// <summary>
/// The Join flow (Mike, 2026-10-02): the player picks one of three prizes, then the combat skill the tournament
/// trains, and only then does the tournament start. Closing either dialog joins nothing and changes nothing: the
/// prize is written and the skill remembered at the final pick, each re-checked against what was offered.
/// </summary>
public sealed class TournamentJoinService
{
    private readonly ITournamentJoinAdapter _join;
    private readonly ITournamentService _arena;
    private readonly ITournamentChoicePresenter _presenter;
    private readonly TournamentRewardsService _rewards;

    public TournamentJoinService(
        ITournamentJoinAdapter join,
        ITournamentService arena,
        ITournamentChoicePresenter presenter,
        TournamentRewardsService rewards)
    {
        _join = join;
        _arena = arena;
        _presenter = presenter;
        _rewards = rewards;
    }

    /// <summary>Shows the choices; <paramref name="proceed"/> runs vanilla's join once both are made.</summary>
    public void BeginJoin(Action proceed)
    {
        var tournament = _join.GetCurrentTournament();
        if (tournament == null)
        {
            proceed();
            return;
        }

        var prizes = tournament.PrizeItemId == null
            ? Array.Empty<string>()
            : _arena.PrizeChoices(tournament.CultureId, tournament.PrizeItemId, tournament.SeedKey).ToArray();
        if (prizes.Length < 2)
        {
            ChooseSkill(tournament, null, proceed);
            return;
        }

        _presenter.ShowPrizeChoice(prizes,
            picked => ChooseSkill(tournament, prizes.Contains(picked) ? picked : null, proceed),
            () => { });
    }

    private void ChooseSkill(TournamentJoinSnapshot tournament, string? prize, Action proceed)
    {
        _presenter.ShowSkillChoice(TournamentRewardRules.CombatSkillIds, skill =>
        {
            if (!TournamentRewardRules.CombatSkillIds.Contains(skill))
                return;
            if (prize != null && prize != tournament.PrizeItemId)
                _join.SetPrize(tournament.TownId, prize);
            _rewards.RememberSkillChoice(tournament.TownId, skill);
            proceed();
        }, () => { });
    }
}
