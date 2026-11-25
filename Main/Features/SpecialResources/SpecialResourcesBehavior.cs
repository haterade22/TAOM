using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.SpecialResources;

public class SpecialResourcesBehavior : CampaignBehaviorBase
{
    private readonly ISpecialResourceService _service;
    private readonly ISpecialResourceStorageService _storage;
    private readonly IModLogger _logger;
    private readonly SpecialResourceEarningService _earning;
    private readonly ISpecialResourceEventAdapter _events;

    public SpecialResourcesBehavior(
        ISpecialResourceService service,
        ISpecialResourceStorageService storage,
        IModLogger logger,
        SpecialResourceEarningService earning,
        ISpecialResourceEventAdapter events)
    {
        _service = service;
        _storage = storage;
        _logger = logger;
        _earning = earning;
        _events = events;
    }

    private PartyScreenLogic _activePartyScreenLogic;

    // The last of the ten OnCharacterCreationIsOverEvent phases, so it runs after v1.5.0's
    // Advanced Starting Options has applied the player start at phase 8.
    private const int CharacterCreationFinalizePhase = 9;

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickHeroEvent.AddNonSerializedListener(this, OnDailyTickHero);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(this, OnRaidCompleted);
        CampaignEvents.OnPrisonerTakenEvent.AddNonSerializedListener(this, OnPrisonerTaken);
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        // Resource seeding fires at culture-FINALIZE, not new-game-create: OnNewGameCreated and
        // OnSessionLaunched both run while the character-creation PLACEHOLDER culture is still on
        // Hero.MainHero.Culture, so seeding there seeds the wrong faction's resource (2026-06-25 bug —
        // a Gondor pick was seeded Tribal Relics and never got Castar). CC-over = new game (culture set);
        // OnGameLoaded = existing/legacy save (culture already correct).
        CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, OnCharacterCreationIsOver);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
        CampaignEvents.OnHideoutBattleCompletedEvent.AddNonSerializedListener(this, OnHideoutCompleted);
        // Player-only recruit charge: OnUnitRecruited fires only from player-facing recruit flows
        // (recruit-volunteers screen, mercenary, prisoners), NOT the AI/generic ApplyInternal path
        // (that fires OnTroopRecruited). Charges the one-time recruit_cost for any troop that carries
        // one (elephant/spider). The RecruitmentVM gate (Patch51) blocks confirming an unaffordable
        // cart, so this deduction never drives the balance negative for those troops.
        CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this, OnUnitRecruited);
        // ScreenManager.OnPushScreen is a static event: it outlives every campaign and keeps each
        // subscribed delegate's target alive, so an orphaned handler is never garbage-collected. A
        // behavior instance is built on every campaign start or save load, and a load without a Game
        // Over leaves the previous instance subscribed, still calling BeginPartyScreenSession on the
        // shared singleton service (#133 P1, #771). So the behavior keeps exactly one live handler per
        // process: remove the stored one, then store and add this instance's.
        if (_screenPushHandler != null)
            ScreenManager.OnPushScreen -= _screenPushHandler;
        _screenPushHandler = OnScreenPushed;
        ScreenManager.OnPushScreen += _screenPushHandler;
        CampaignEvents.OnGameOverEvent.AddNonSerializedListener(this, UnsubscribeScreenManager);
    }

    private static ScreenManager.OnPushScreenEvent _screenPushHandler;

    private void UnsubscribeScreenManager()
    {
        if (_screenPushHandler == null) return;
        ScreenManager.OnPushScreen -= _screenPushHandler;
        _screenPushHandler = null;
        _logger.LogInfo("[SpecRes] OnGameOver - unsubscribed from ScreenManager.OnPushScreen");
    }

    public override void SyncData(IDataStore dataStore)
    {
        _logger.LogInfo("[SpecRes] SyncData called (save/load)");
        if (dataStore.IsSaving)
        {
            var saved = _storage.GetAllData();
            dataStore.SyncData("_taom_specialResources", ref saved);
            return;
        }

        // Null-first on load: a missing key leaves the ref unchanged, so seeding it from the live
        // singleton would hand the previous campaign's balances straight back (plan 001). A save
        // with no record for this behavior never reaches SyncData at all; that load gap is still
        // open (special-resources.md, SyncData persistence).
        Dictionary<string, float> data = null;
        dataStore.SyncData("_taom_specialResources", ref data);
        _storage.RestoreData(data);
        _logger.LogInfo($"[SpecRes] SyncData restored {data?.Count ?? 0} entries");
        // Phase 9b #133 P1 — pre-fix called _storage.ClampAll(playerResource.Cap) here. That
        // applied the PLAYER's current resource cap to EVERY key in the dict (multi-hero,
        // multi-resource saves). Gems (cap 600) got clamped to War Spoils' 500; Elven Wine
        // clamped to 500 instead of 400. SyncData should be pure round-trip — per-resource
        // cap belongs inside RestoreData/Set (keyed by resource), not here.
    }

    // Internal for unit-test reach. Reads no hero: neither reset below needs one.
    internal void OnNewGameCreated(CampaignGameStarter starter)
    {
        // Plan 001: the storage is a process-lifetime singleton and a new game never runs the
        // SyncData load, so campaign B would keep campaign A's balances and write them into B's
        // first save. Wipe it before the character-creation finalize seeds B.
        _storage.RestoreData(null);

        // Phase 9b deferred #133 P2 R1 — clear singleton-scope service state so a second
        // campaign in the same process can't inherit _inSession / _pendingSpend from the
        // prior campaign. Must run BEFORE any seeding so stale _loggedResolveKeys dedupe
        // entries don't suppress the new campaign's first-resolve diagnostics.
        _service.ResetSessionState();
        // Seeding is deliberately NOT done here: at OnNewGameCreated the chosen culture is not yet
        // applied to Hero.MainHero.Culture (still the character-creation placeholder), so seeding now
        // seeds the wrong faction's resource. It happens at OnCharacterCreationIsOver instead. (2026-06-25.)
    }

    // New game: the player's culture is finalized by the time character creation completes (Patch29 /
    // CultureSettingService set Hero.MainHero.Culture during the culture stage). Seed the starting
    // resource now, with the correct culture. InitializeHero is the existing (tested) unconditional
    // seed — correct here because a freshly-created hero is untracked.
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
        GetHeroIds(hero, out var kingdomId, out var cultureId);
        _service.InitializeHero(hero.StringId, kingdomId, cultureId);
        _logger.LogInfo($"SpecialResources: Seeded starting resource for {hero.Name} at character-creation finalize");
    }

    // Existing/legacy save: culture is already finalized. Contains-gated backstop so a save predating
    // SpecialResources (or a hero never seeded) gets its starting amount once, without ever overwriting
    // an earned/spent balance (the #133 P2 gate). On a new game this is a no-op (CC-over already seeded).
    private void OnGameLoaded(CampaignGameStarter starter)
    {
        var hero = Hero.MainHero;
        if (hero == null) return;
        GetHeroIds(hero, out var kingdomId, out var cultureId);
        _earning.SeedIfUntracked(hero.StringId, kingdomId, cultureId, $"legacy save ({hero.Name})");
    }

    // The desertion grace (#133 P2) lives in the earning service, which is new for every campaign.
    // Seeding is not done on OnSessionLaunched: it fires while character creation is still in progress
    // (placeholder culture), which seeded the wrong faction's resource (2026-06-25 seeding-culture bug).
    private void OnDailyTickHero(Hero hero)
    {
        var input = _events.SnapshotDaily(hero, _earning.SessionActive);
        if (input == null) return;

        var outcome = _earning.OnDailyTick(input);
        if (!outcome.Processed) return;

        SpecialResourceToasts.Daily(outcome);

        var deserted = outcome.Desertions.Count > 0 ? _events.ApplyDesertion(hero, outcome.Desertions) : 0;
        if (deserted > 0) SpecialResourceToasts.Desertion(deserted, outcome.ResourceName);
    }

    // A null snapshot means the adapter found nothing to credit on this process.
    private void Pay<T>(T snapshot, System.Func<T, IReadOnlyList<EarningNotice>> earn) where T : class
    {
        if (snapshot != null) SpecialResourceToasts.Earnings(earn(snapshot));
    }

    private void OnMapEventEnded(MapEvent mapEvent)
        => Pay(_events.SnapshotVictory(mapEvent), _earning.OnVictory);

    private void OnRaidCompleted(BattleSideEnum side, RaidEventComponent component)
        => Pay(_events.SnapshotRaid(side, component), _earning.OnRaid);

    private void OnPrisonerTaken(FlattenedTroopRoster roster)
        => SpecialResourceToasts.Earnings(_earning.OnPrisoners(_events.DescribeLocalHero(), _events.CountPrisoners(roster)));

    private void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
        => SpecialResourceToasts.Earnings(_earning.OnTournament(_events.DescribeTournamentWinner(winner)));

    // v1.4.3 added the 3rd param HideoutBattleEndState; we don't act on it (any attacker win
    // with a player map event still earns the resource).
    private void OnHideoutCompleted(BattleSideEnum winnerSide, HideoutEventComponent component, HideoutEventComponent.HideoutBattleEndState battleEndState)
        => Pay(_events.SnapshotHideout(winnerSide, component), _earning.OnHideout);

    // Charges the one-time recruit_cost (no-op unless the troop carries one in troop_resource_costs.xml; Patch51
    // gates volunteers and Patch26 party-screen prisoners outside co-op, #563; a guest's gate reads a stale balance, #967). The service picks
    // the payer: Hero.MainHero in single-player, the leader of the party Patch113 names under co-op.
    private void OnUnitRecruited(CharacterObject character, int count)
    {
        if (character == null || count <= 0) return;

        var context = _events.SnapshotRecruit();
        if (context == null) return;

        var notice = _earning.ChargeRecruit(context, character.StringId, count);
        if (notice == null) return;

        SpecialResourceToasts.RecruitCharge(notice, count, character.Name?.ToString());
    }

    private void OnScreenPushed(ScreenBase screen)
    {
        if (screen?.GetType().Name != "GauntletPartyScreen") return;

        _service.BeginPartyScreenSession();
    }

    public void AttachToPartyScreen(PartyScreenLogic logic)
    {
        if (_activePartyScreenLogic == logic) return;
        DetachFromPartyScreen();
        _activePartyScreenLogic = logic;
        _activePartyScreenLogic.PartyScreenClosedEvent += OnPartyScreenClosed;
        _activePartyScreenLogic.AfterReset += OnPartyScreenReset;
    }

    private void DetachFromPartyScreen()
    {
        if (_activePartyScreenLogic == null) return;
        _activePartyScreenLogic.PartyScreenClosedEvent -= OnPartyScreenClosed;
        _activePartyScreenLogic.AfterReset -= OnPartyScreenReset;
        _activePartyScreenLogic = null;
    }

    private void OnPartyScreenClosed(PartyBase leftOwner, TroopRoster leftMembers, TroopRoster leftPrisoners,
        PartyBase rightOwner, TroopRoster rightMembers, TroopRoster rightPrisoners, bool fromCancel)
    {
        DetachFromPartyScreen();
        if (fromCancel)
        {
            _service.CancelSession();
        }
        else
        {
            var hero = Hero.MainHero;
            GetHeroIds(hero, out var kingdomId, out var cultureId);
            var spent = _service.CommitSession(hero?.StringId, kingdomId, cultureId);
            if (spent > 0f && hero != null)
            {
                var resource = _service.ResolveResource(kingdomId, cultureId);
                var balance = _service.GetCurrentAmount(hero.StringId, kingdomId, cultureId);
                SpecialResourceToasts.UpgradeSpend(resource?.DisplayName, spent, balance);
            }
        }
    }

    private void OnPartyScreenReset(PartyScreenLogic logic, bool fromCancel)
    {
        _service.RestartPartyScreenSession(Hero.MainHero?.StringId, Hooks.PartyScreenHeldUpgrades.Read(logic));
    }

    private static void GetHeroIds(Hero hero, out string kingdomId, out string cultureId)
    {
        kingdomId = hero?.Clan?.Kingdom?.StringId;
        cultureId = hero?.Culture?.StringId;
    }
}
