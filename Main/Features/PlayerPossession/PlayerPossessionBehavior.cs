using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.PlayerPossession;

/// <summary>
/// Watches for the controlled-hero switch a multiplayer join performs, and re-applies the
/// character-creation package to the hero the player actually ends up controlling.
///
/// Thin by ADR-002: this is the boundary that converts sealed <see cref="Hero"/> state into ids and
/// hands them to the services. It is inert in single-player, where <c>Hero.MainHero</c> never
/// changes and the co-op presence gate never opens.
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
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, _ => RecordBaseline());
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => RecordBaseline());
        CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, OnCharacterCreationIsOver);
        // Hourly rather than per-tick: the hand-off happens once at join, and an hourly check costs
        // one string comparison. Nothing here is time-sensitive — the grants are idempotent-by-marker.
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
