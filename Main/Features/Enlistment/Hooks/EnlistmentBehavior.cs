using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Thin lifecycle boundary (ADR-002): SyncData, load normalization, the hourly reconcile
/// tick, and captivity edges. All decisions live in the services; this class only routes.
/// </summary>
public class EnlistmentBehavior : CampaignBehaviorBase
{
    private const string SaveKey = "_taom_enlistment";

    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentStateMachine _machine;
    private readonly IEnlistmentReconciler _reconciler;
    private readonly IEnlistmentLoadNormalizer _normalizer;
    private readonly IPlayerPartyAdapter _playerParty;
    private readonly ICoopSessionProvider _coopSession;
    private readonly IServiceMaintenanceService _maintenance;
    private readonly IModLogger _logger;

    private CampaignGameStarter _lastSessionStarter;
    private bool _justLoadedFromSave;

    public EnlistmentBehavior(
        IEnlistmentStore store,
        IEnlistmentStateMachine machine,
        IEnlistmentReconciler reconciler,
        IEnlistmentLoadNormalizer normalizer,
        IPlayerPartyAdapter playerParty,
        ICoopSessionProvider coopSession,
        IServiceMaintenanceService maintenance,
        IModLogger logger)
    {
        _store = store;
        _machine = machine;
        _reconciler = reconciler;
        _normalizer = normalizer;
        _playerParty = playerParty;
        _coopSession = coopSession;
        _maintenance = maintenance;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
        CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
    }

    // Event-driven captivity sync — closes the wait-menu-on-captive-party latency window
    // of the hourly-only design (CommanderUnavailable is skipped: its grace freezes instead).
    private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
    {
        if (!_coopSession.IsAuthority || !_store.Record.IsEnlisted)
            return;
        if (prisoner?.StringId != _store.Record.EnlistedHeroId)
            return;
        if (_store.Record.State != EnlistmentState.EnlistedPlayerCaptive
            && _store.Record.State != EnlistmentState.CommanderUnavailable)
        {
            _machine.TryTransition(EnlistmentState.EnlistedPlayerCaptive);
        }
    }

    private void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction capturerFaction, EndCaptivityDetail detail, bool showNotification)
    {
        if (!_coopSession.IsAuthority)
            return;
        if (prisoner?.StringId != _store.Record.EnlistedHeroId)
            return;
        if (_store.Record.State == EnlistmentState.EnlistedPlayerCaptive)
            _reconciler.ReconcileHourly(CampaignTime.Now.ToDays);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
        {
            var snapshot = _store.Serialize();
            dataStore.SyncData(SaveKey, ref snapshot);
        }
        else
        {
            Dictionary<string, string> snapshot = null;
            dataStore.SyncData(SaveKey, ref snapshot);
            _store.Deserialize(snapshot);
            _justLoadedFromSave = true; // tells OnSessionLaunched NOT to clear the freshly-loaded store
        }
    }

    // CO-OP: host-only. The reconciler parks/restores the shared MainParty and executes
    // discharges — client-side reconciliation would fight the host over both.
    // internal for TAOM.Tests (InternalsVisibleTo) — lets the co-op authority gate be asserted directly.
    internal void OnHourlyTick()
    {
        if (!_coopSession.IsAuthority) return;
        _reconciler.ReconcileHourly(CampaignTime.Now.ToDays);
    }

    // CO-OP: host-only. Normalization discharges/parks — world mutations a client must
    // not apply; the client loads the host's already-normalized record.
    // internal for TAOM.Tests (InternalsVisibleTo).
    internal void OnGameLoaded(CampaignGameStarter starter)
    {
        if (!_coopSession.IsAuthority) return;

        // BEFORE normalizing — it owns every per-session cache (stale commander id, stale Army handle).
        _maintenance.ResetSessionCaches();
        _normalizer.Normalize(_playerParty.GetMainHeroId(), CampaignTime.Now.ToDays);
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        // A brand-new campaign starts with no service record. SyncData(IsLoading) has NOT
        // run here, so _justLoadedFromSave is false and clearing is correct.
        if (!_justLoadedFromSave)
            _store.Clear();
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // Singleton behavior survives across campaigns within one process. On a genuinely
        // new starter clear the store — UNLESS SyncData just loaded a save.
        if (_lastSessionStarter != starter)
        {
            _lastSessionStarter = starter;
            if (!_justLoadedFromSave)
                _store.Clear();
        }

        // Clear unconditionally (same-process save→load→save→load reuses the starter on
        // the 2nd load — the Messengers #123 stuck-flag lesson).
        _justLoadedFromSave = false;
    }
}
