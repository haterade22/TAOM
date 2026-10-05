using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.TournamentRewards;

/// <summary>
/// Notes how many heroes fought a finished tournament, pays the player's chosen skill when their tournament ends,
/// and clears the reward service's transient state at each session launch. Saves nothing: a choice lives from Join
/// to the end of that tournament's mission.
/// <para>
/// The ORDER of the TournamentFinished listeners is load-bearing. Vanilla's own handler
/// (<c>TournamentCampaignBehavior.OnTournamentFinished</c>) asks the model for the winner's renown and influence, and
/// the model reads the hero count noted here, so this handler must run first. On the installed v1.5.3 it does, and
/// nothing but the engine's own order keeps it so. Re-read these on an engine bump; a test pins only the first
/// (TournamentRewardsBindingTests.MbEvent_RunsTheLastRegisteredListenerFirst):
/// </para>
/// <list type="number">
/// <item>An <c>MbEvent</c> makes the newest listener the head of its list and invokes from the head, so the
/// last-registered listener runs first (<c>MbEvent`4.cs</c>: AddNonSerializedListener, :24-28, InvokeList, :37).</item>
/// <item><c>CampaignBehaviorManager.RegisterEvents</c> registers each behavior's events in list order
/// (<c>CampaignBehaviorManager.cs</c>:32-38, reached at Campaign.cs:1449 for a loaded save and :1645 for a new
/// campaign), and the list is the starter's, appended by <c>CampaignGameStarter.AddBehavior</c> (:35-41).</item>
/// <item>The vanilla behavior is appended first: <c>Campaign.OnInitialize</c> calls <c>SandBoxManager.Initialize</c>
/// (Campaign.cs:1403), which adds <c>TournamentCampaignBehavior</c> (SandBoxManager.cs:70), before
/// <c>GameManager.OnGameStart</c> (:1410) runs each SubModule's <c>OnGameStart</c> (MBGameManager.cs:159-168), where
/// the feature modules add this behavior (SubModule.OnGameStart, FeatureModuleHooks.AddGameStartContent).</item>
/// <item>Vanilla subscribes to the same public event (<c>TournamentCampaignBehavior.cs</c>:27) and
/// <c>CampaignEvents.OnTournamentFinished</c> invokes it (:1690), for a played or watched tournament
/// (<c>TournamentBehavior.EndCurrentMatch</c>) and an off-screen one (<c>TournamentManager.ResolveTournament</c>).</item>
/// </list>
/// <para>
/// <c>MbEvent</c> invokes its listeners with no try, so a fault here would also skip vanilla's handler and the winner's
/// renown and influence with it: the handler catches, logs and lets the rest of the event run.
/// </para>
/// </summary>
public sealed class TournamentRewardsBehavior : CampaignBehaviorBase
{
    private const string Tag = "[TournamentRewards]";

    private readonly TournamentRewardsService _rewards;
    private readonly TournamentSkillAwardService _award;
    private readonly IModLogger _logger;

    public TournamentRewardsBehavior(TournamentRewardsService rewards, TournamentSkillAwardService award, IModLogger logger)
    {
        _rewards = rewards;
        _award = award;
        _logger = logger;
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

    // Internal for the fault-isolation test.
    internal void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
    {
        try
        {
            var townId = town?.Settlement?.StringId;
            _rewards.NoteTournamentFinished(townId, participants?.Count(p => p != null && p.IsHero) ?? 0);
            var player = CharacterObject.PlayerCharacter;
            _award.OnTournamentFinished(winner != null && winner == player, townId,
                player?.HeroObject?.StringId, player?.HeroObject?.Culture?.StringId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{Tag} the tournament-finished handler failed ({ex.GetType().Name}: {ex.Message}); vanilla's rewards go on");
        }
    }

    private void OnPlayerEliminated(int round, Town town)
    {
        var hero = CharacterObject.PlayerCharacter?.HeroObject;
        _award.OnPlayerEliminated(round, town?.Settlement?.StringId, hero?.StringId, hero?.Culture?.StringId);
    }
}
