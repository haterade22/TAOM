using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Siege;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.Siege;

public class SiegeDefenseBehavior : CampaignBehaviorBase
{
    private readonly ISiegeDefenseService _service;
    private readonly IModLogger _logger;

    // Phase 9b #132 — flat primitive dict used as SyncData transport. Mirrors
    // CareerPersistenceBehavior pattern (avoids SaveableTypeDefiner).
    // Format: settlementId -> "defenderFactionId|remainingHours|accepted|rewardClaimed"
    private Dictionary<string, string> _activeEventsForSave = new Dictionary<string, string>();

    private readonly ICoopSessionProvider _coopSession;

    public SiegeDefenseBehavior(ISiegeDefenseService service, IModLogger logger, ICoopSessionProvider coopSession)
    {
        _service = service;
        _logger = logger;
        _coopSession = coopSession;
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

    // CO-OP: host-only, as a whole.
    //
    // This was briefly split (2026-08-01) into a host-only timer sweep plus a per-peer reward, on
    // the theory that the reward is keyed on Hero.MainHero and so is legitimately per-player like
    // CareerQuestCampaignBehavior. That was wrong, and Codex caught it: the reward's preconditions,
    // PlayerAccepted and RewardClaimed, are fields on the SHARED _activeEvents entries serialised
    // into _taom_siege_active_events — and a joining client's baseline for that key is the HOST's
    // save. So a client would inherit the host's acceptance and claim a reward it never earned, or
    // be blocked by a claim the host already made. "Keyed on MainHero" was true of the payout and
    // false of the decision to pay out.
    //
    // Gating the reward on IsAuthority instead produces behaviour identical to gating the whole
    // tick, so the split bought nothing and was reverted. A co-op client correctly earning this
    // needs per-peer accept/claim state — a feature change, not a gate placement. Known limitation
    // in docs/features/coop-interop.md.
    // internal for TAOM.Tests (InternalsVisibleTo) — lets the co-op authority gate be asserted directly.
    internal void OnHourlyTick()
    {
        // SHARED half — expires events and prunes the save-backed _activeEvents. Authority only.
        if (_coopSession.IsAuthority)
            _service.OnHourlyTickShared();

        // LOCAL half — grants the reward to whichever hero THIS peer plays. Every peer, same
        // reasoning that leaves CareerQuestCampaignBehavior ungated. Gating both meant a co-op
        // client could defend a siege to completion and receive nothing.
        _service.OnHourlyTickLocalPlayer();
    }
}
