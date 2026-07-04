using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// Thin entry point (ADR-002): routes campaign events through the snapshot adapter into
/// the momentum services, persists via the state store's flat dict, and shows the
/// victory inquiry. Victory checks run on daily tick + kingdom destruction (LOTRAOM
/// parity — mid-day threshold crossings wait for the next tick).
/// </summary>
public class WarOfTheRingMomentumBehavior : CampaignBehaviorBase
{
    private readonly IMomentumStateStore _stateStore;
    private readonly IMomentumEventService _eventService;
    private readonly IMomentumEnrollmentService _enrollmentService;
    private readonly IMomentumVictoryService _victoryService;
    private readonly IWarEventSnapshotAdapter _snapshotAdapter;
    private readonly IMomentumSettingsProvider _settings;
    private readonly IWarOfTheRingService _wotrService;
    private readonly UI.IMomentumUIService _uiService;
    private readonly IModLogger _logger;

    private Dictionary<string, string> _persisted = new();

    public WarOfTheRingMomentumBehavior(
        IMomentumStateStore stateStore,
        IMomentumEventService eventService,
        IMomentumEnrollmentService enrollmentService,
        IMomentumVictoryService victoryService,
        IWarEventSnapshotAdapter snapshotAdapter,
        IMomentumSettingsProvider settings,
        IWarOfTheRingService wotrService,
        UI.IMomentumUIService uiService,
        IModLogger logger)
    {
        _stateStore = stateStore;
        _eventService = eventService;
        _enrollmentService = enrollmentService;
        _victoryService = victoryService;
        _snapshotAdapter = snapshotAdapter;
        _settings = settings;
        _wotrService = wotrService;
        _uiService = uiService;
        _logger = logger;
    }

    private static double NowHours => CampaignTime.Now.ToHours;

    public override void RegisterEvents()
    {
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => _stateStore.ResetForNewGame());
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        CampaignEvents.SiegeCompletedEvent.AddNonSerializedListener(this, OnSiegeCompleted);
        CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(this, OnRaidCompleted);
        CampaignEvents.ArmyGathered.AddNonSerializedListener(this, OnArmyGathered);
        CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, OnKingdomDestroyed);
        // The map meter needs a live MapScreen — OnSessionLaunched is too early on load.
        CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, RefreshMapMeter);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
            _persisted = _stateStore.Serialize();

        dataStore.SyncData("_taom_wotr_momentum", ref _persisted);

        if (dataStore.IsLoading)
            _stateStore.Deserialize(_persisted);
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // Load-reconcile: a save written mid-victory (or by a pre-fix build) can carry an
        // ended momentum war while the phase machine still says FullWar. EndWar is idempotent.
        var state = _stateStore.State;
        if (state.HasWarEnded && _wotrService.CurrentPhase != WarPhase.WarEnded)
        {
            _logger.LogInfo("[Momentum] Load-reconcile: momentum war ended but phase machine wasn't — ending war");
            _wotrService.EndWar(state.Victor);
        }

        if (_settings.MomentumEnabled)
            _enrollmentService.SweepEnrollment(state);
    }

    private void OnDailyTick()
    {
        if (!_settings.MomentumEnabled)
            return;

        var state = _stateStore.State;
        _enrollmentService.SweepEnrollment(state);
        if (!state.HasWarStarted || state.HasWarEnded)
            return;

        var outcome = _victoryService.CheckAndApplyVictory(state);
        if (outcome != WarOutcome.None)
        {
            ShowVictoryInquiry(outcome);
            _stateStore.NotifyMomentumChanged();
            RefreshMapMeter();
            return;
        }

        _eventService.ProcessDailyTick(state, NowHours);
        _stateStore.NotifyMomentumChanged();
        RefreshMapMeter();
    }

    // Adds the meter once the war is live; removes it after victory. Idempotent —
    // MomentumUIService no-ops when the view is already in the wanted state.
    private void RefreshMapMeter()
    {
        var state = _stateStore.State;
        bool wantMeter = _settings.MomentumEnabled && state.HasWarStarted && !state.HasWarEnded;

        if (wantMeter)
            _uiService.AddMapMeter();
        else
            _uiService.RemoveMapMeter();
    }

    private void OnMapEventEnded(MapEvent mapEvent)
    {
        if (!_settings.MomentumEnabled)
            return;

        var snapshot = _snapshotAdapter.FromMapEvent(mapEvent);
        if (snapshot == null)
            return;

        _eventService.ProcessBattle(snapshot, _stateStore.State, NowHours);
        _stateStore.NotifyMomentumChanged();
    }

    private void OnSiegeCompleted(Settlement settlement, MobileParty capturerParty, bool isWin, MapEvent.BattleTypes battleType)
    {
        if (!isWin || !_settings.MomentumEnabled)
            return;

        var snapshot = _snapshotAdapter.FromSiege(settlement, capturerParty);
        if (snapshot == null)
            return;

        _eventService.ProcessSiege(snapshot, _stateStore.State, NowHours);
        _stateStore.NotifyMomentumChanged();
    }

    private void OnRaidCompleted(BattleSideEnum winnerSide, RaidEventComponent component)
    {
        if (!_settings.MomentumEnabled)
            return;

        var snapshot = _snapshotAdapter.FromRaid(winnerSide, component);
        if (snapshot == null)
            return;

        _eventService.ProcessRaid(snapshot, _stateStore.State, NowHours);
        _stateStore.NotifyMomentumChanged();
    }

    private void OnArmyGathered(Army army, IMapPoint gatheringPoint)
    {
        if (!_settings.MomentumEnabled)
            return;

        var snapshot = _snapshotAdapter.FromArmyGathered(army);
        if (snapshot == null)
            return;

        _eventService.ProcessArmyGathered(snapshot, _stateStore.State, NowHours);
        _stateStore.NotifyMomentumChanged();
    }

    private void OnKingdomDestroyed(Kingdom kingdom)
    {
        if (!_settings.MomentumEnabled)
            return;

        var state = _stateStore.State;
        if (!_enrollmentService.RemoveKingdom(state, kingdom?.StringId))
            return;

        var outcome = _victoryService.CheckAndApplyVictory(state);
        if (outcome != WarOutcome.None)
            ShowVictoryInquiry(outcome);

        _stateStore.NotifyMomentumChanged();
    }

    private static void ShowVictoryInquiry(WarOutcome outcome)
    {
        var body = outcome == WarOutcome.FreeVictory
            ? new TextObject("{=taom_wotr_victory_free}The war is over. The Free Peoples have persevered, and won the future for their children. Long live freedom!")
            : new TextObject("{=taom_wotr_victory_evil}The realms of Men and Elf alike have been crushed. Long live Sauron!");

        InformationManager.ShowInquiry(new InquiryData(
                new TextObject("{=taom_wotr_victory_title}End of the War").ToString(),
                body.ToString(),
                isAffirmativeOptionShown: true,
                isNegativeOptionShown: false,
                affirmativeText: new TextObject("{=taom_wotr_continue}Continue").ToString(),
                negativeText: "",
                affirmativeAction: () => { },
                negativeAction: () => { }),
            pauseGameActiveState: true,
            prioritize: false);
    }
}
