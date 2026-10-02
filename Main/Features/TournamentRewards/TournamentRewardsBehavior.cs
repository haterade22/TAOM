using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Features.TournamentRewards;

/// <summary>
/// Pays the player's chosen tournament skill when their tournament ends, and clears the reward service's
/// transient state at each session launch. Saves nothing: a choice lives from Join to the end of that
/// tournament's mission. Campaign-event listeners run last-registered first, so this runs before vanilla's
/// TournamentFinished handlers; it touches nothing they read.
/// </summary>
public sealed class TournamentRewardsBehavior : CampaignBehaviorBase
{
    private readonly TournamentRewardsService _rewards;
    private readonly TournamentSkillAwardService _award;

    public TournamentRewardsBehavior(TournamentRewardsService rewards, TournamentSkillAwardService award)
    {
        _rewards = rewards;
        _award = award;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
        CampaignEvents.PlayerEliminatedFromTournament.AddNonSerializedListener(this, OnPlayerEliminated);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnSessionLaunched(CampaignGameStarter starter) => _rewards.ResetForNewSession();

    private void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
    {
        var player = CharacterObject.PlayerCharacter;
        _award.OnTournamentFinished(winner != null && winner == player, town?.Settlement?.StringId,
            player?.HeroObject?.StringId, player?.HeroObject?.Culture?.StringId);
    }

    private void OnPlayerEliminated(int round, Town town)
    {
        var hero = CharacterObject.PlayerCharacter?.HeroObject;
        _award.OnPlayerEliminated(round, town?.Settlement?.StringId, hero?.StringId, hero?.Culture?.StringId);
    }
}
