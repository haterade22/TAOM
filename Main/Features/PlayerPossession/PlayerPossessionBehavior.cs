using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.PlayerPossession;

/// <summary>
/// Watches for the controlled-hero switch a multiplayer join performs, and re-applies the
/// character-creation package to the hero the player actually ends up controlling.
///
/// Thin by ADR-002: this is the boundary that converts sealed <see cref="Hero"/> state into ids and
/// hands them to the services. With no co-op module enabled it is inert: the co-op presence gate never
/// opens. <c>Hero.MainHero</c> does change in solo play (heir succession, the Player Switcher hand-over);
/// see #1183.
/// </summary>
public class PlayerPossessionBehavior : CampaignBehaviorBase
{
    private readonly IPlayerPossessionService _possession;
    private readonly IJoinReconciliationService _reconciliation;
    private readonly ICareerMenuService _careerMenu;
    private readonly IModLogger _logger;

    /// <summary>
    /// Heroes already reconciled, persisted with the save. A reconnect restores this set, so a
    /// player who rejoins cannot be handed a second starting package.
    /// </summary>
    private List<string> _reconciledHeroIds = new();

    public PlayerPossessionBehavior(
        IPlayerPossessionService possession,
        IJoinReconciliationService reconciliation,
        ICareerMenuService careerMenu,
        IModLogger logger)
    {
        _possession = possession;
        _reconciliation = reconciliation;
        _careerMenu = careerMenu;
        _logger = logger;
    }

    // The last of the ten OnCharacterCreationIsOverEvent phases, so it runs after v1.5.0's
    // Advanced Starting Options has applied the player start at phase 8.
    private const int CharacterCreationFinalizePhase = 9;

    public override void RegisterEvents()
    {
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => _possession.ResetForNewCampaign());
        CampaignEvents.OnCharacterCreationInitializedEvent.AddNonSerializedListener(this, _ => _possession.BeginCharacterCreation());
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, _ => RecordBaseline());
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => RecordBaseline());
        CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, OnCharacterCreationIsOver);
        // Coop's join hand-off runs ChangePlayerCharacterAction.Apply. Coop's prefix replaces the vanilla
        // body and raises this event itself (Coop bannerlord-1.5.x 995517e60,
        // ChangePlayerCharacterActionPatches.cs:57). The hourly event needs a running clock, and a joining
        // client's clock can stand still for minutes.
        CampaignEvents.OnPlayerCharacterChangedEvent.AddNonSerializedListener(this, OnPlayerCharacterChanged);
        // The fallback for a co-op hand-off that raises no player-character-changed event.
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, CheckForPossession);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Clear-on-load: SyncData leaves the ref value UNCHANGED for an absent key, so loading a
        // pre-feature save after a reconciled one in the same process would inherit the previous
        // campaign's marker list and silently suppress a legitimate grant.
        if (dataStore.IsLoading) _reconciledHeroIds = new List<string>();
        dataStore.SyncData("_taom_possessionReconciledHeroes", ref _reconciledHeroIds);
    }

    private void RecordBaseline() => _possession.RecordBaselineHero(Hero.MainHero?.StringId);

    internal void OnCharacterCreationIsOver(int index)
    {
        // v1.5.0: OnCharacterCreationIsOverEvent became MbEvent<int> and the dispatcher now
        // fires it TEN times (index 0..9) as a phase-ordering mechanism. Every vanilla subscriber
        // guards on the index; an unguarded handler would run ten times. Phase 8 is where
        // Advanced Starting Options applies the chosen player start (king / vassal / mercenary /
        // trader / outlaw / beggar), which can hand the player a kingdom, fiefs, a workshop or a
        // caravan, so anything that reads the settled player state must run after it. 9 is last.
        if (index != CharacterCreationFinalizePhase) return;

        var hero = Hero.MainHero;
        if (hero == null) return;

        _possession.CaptureCharacterCreationChoices(new PlayerCharacterCreationChoices(
            hero.StringId,
            hero.Culture?.StringId,
            hero.CharacterObject?.Race ?? -1,
            _careerMenu?.SelectedCareerStringId));
    }

    // Runs inside Coop's HeroInterface.SwitchToPlayer, before Coop's own work after the switch (captivity,
    // camera, fog of war, the SwitchedPlayer message). The event dispatch catches nothing, so an escaping
    // exception would end the switch, and the client would never leave Coop's LoadingState.
    // The arguments are ignored on purpose: under Coop, oldPlayer and newPlayer are the same hero, because
    // Coop sets PlayerTroop before it calls Apply. The check reads Hero.MainHero instead.
    internal void OnPlayerCharacterChanged(Hero oldPlayer, Hero newPlayer, MobileParty newMainParty, bool isMainPartyChanged)
    {
        try
        {
            CheckForPossession();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Possession] Checking for a join hand-off at the player switch failed: {ex}");
        }
    }

    internal void CheckForPossession()
    {
        var hero = Hero.MainHero;
        if (hero == null) return;

        if (!_possession.TryConsumePossession(hero.StringId, out var choices)) return;

        if (_reconciledHeroIds.Contains(hero.StringId))
        {
            _logger.LogInfo(
                $"[Possession] '{hero.StringId}' was already reconciled in an earlier session — " +
                "not re-granting the character-creation package.");
            return;
        }

        // Kingdom is resolved HERE, at the boundary: only the host knows which kingdom the joining
        // hero landed in, and the character-creation campaign never had that information.
        var kingdomId = hero.Clan?.Kingdom?.StringId;

        if (_reconciliation.ReapplyCharacterCreationPackage(choices, hero.StringId, kingdomId))
            _reconciledHeroIds.Add(hero.StringId);
    }
}
