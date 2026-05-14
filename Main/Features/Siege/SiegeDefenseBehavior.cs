using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Siege;

namespace TAOM.Features.Siege;

public class SiegeDefenseBehavior : CampaignBehaviorBase
{
    private readonly ISiegeDefenseService _service;
    private readonly IModLogger _logger;

    // Phase 9b #132 — flat primitive dict used as SyncData transport. Mirrors
    // CareerPersistenceBehavior pattern (avoids SaveableTypeDefiner).
    // Format: settlementId -> "defenderFactionId|remainingHours|accepted|rewardClaimed"
    private Dictionary<string, string> _activeEventsForSave = new Dictionary<string, string>();

    public SiegeDefenseBehavior(ISiegeDefenseService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        _logger.LogInfo("[SiegeDefense] SiegeDefenseBehavior registering events");
        // Phase 9b #132 R1 — reset on new game ONLY (fresh campaign). For save load, SyncData fires
        // first with IsLoading=true and calls RestoreFromSave, which clears then repopulates. Using
        // OnSessionLaunchedEvent (which fires for both new + load) would race with SyncData on load.
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, OnSiegeEventStarted);
        CampaignEvents.OnSiegeEventEndedEvent.AddNonSerializedListener(this, OnSiegeEventEnded);
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
            _activeEventsForSave = _service.SnapshotForSave();

        dataStore.SyncData("_taom_siege_active_events", ref _activeEventsForSave);

        if (dataStore.IsLoading && _activeEventsForSave != null)
            _service.RestoreFromSave(_activeEventsForSave);
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        _service.Reset();
    }

    private void OnSiegeEventStarted(SiegeEvent siegeEvent)
    {
        var adapter = new SiegeEventAdapter(siegeEvent);
        _service.OnSiegeStarted(adapter);
    }

    private void OnSiegeEventEnded(SiegeEvent siegeEvent)
    {
        _service.OnSiegeEnded(siegeEvent.BesiegedSettlement?.StringId ?? "");
    }

    private void OnSettlementOwnerChanged(
        TaleWorlds.CampaignSystem.Settlements.Settlement settlement,
        bool opened,
        Hero newOwner,
        Hero oldOwner,
        Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        _service.OnSiegeEnded(settlement.StringId);
    }

    private void OnHourlyTick() => _service.OnHourlyTick();
}
