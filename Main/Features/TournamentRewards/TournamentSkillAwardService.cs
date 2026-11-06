using TAOM.Adapters;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.TournamentRewards;

/// <summary>
/// Pays the skill the player chose at Join when their tournament ends (TournamentRewardsBehavior): a win is all
/// four rounds plus the win bonus; an elimination in round N (0-based, the engine's CurrentRoundIndex) is the N
/// rounds won before it. Nothing on a dedicated server, whose main hero is an idle world-gen hero, and nothing for a
/// co-op guest, whose hero's skills the host owns (#1086).
/// </summary>
public sealed class TournamentSkillAwardService
{
    private readonly TournamentRewardsService _rewards;
    private readonly ITournamentChoicePresenter _presenter;
    private readonly IDedicatedServerProvider _server;
    private readonly ICoopSessionProvider _coop;

    public TournamentSkillAwardService(TournamentRewardsService rewards, ITournamentChoicePresenter presenter,
        IDedicatedServerProvider server, ICoopSessionProvider coop)
    {
        _rewards = rewards;
        _presenter = presenter;
        _server = server;
        _coop = coop;
    }

    public void OnTournamentFinished(bool winnerIsPlayer, string? townId, string? playerHeroId, string? playerCultureId)
    {
        if (winnerIsPlayer)
            Award(townId, TournamentRewardRules.Rounds, true, playerHeroId, playerCultureId);
    }

    public void OnPlayerEliminated(int roundIndex, string? townId, string? playerHeroId, string? playerCultureId) =>
        Award(townId, roundIndex, false, playerHeroId, playerCultureId);

    private void Award(string? townId, int roundsWon, bool won, string? playerHeroId, string? playerCultureId)
    {
        if (_server.IsDedicatedServer || _coop.ShouldDeferToHost)
            return;
        var skill = _rewards.ChosenSkill(townId);
        var xp = _rewards.AwardPlayerSkillXp(townId, roundsWon, won, playerHeroId, playerCultureId);
        if (skill != null && xp > 0)
            _presenter.ShowSkillTrained(skill);
    }
}
