using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Features.CultureConversion.Hooks;

/// <summary>
/// Thin event router (ADR-002) for gradual culture conversion. All decisions live in
/// <see cref="ICultureConversionService"/>; this class wires campaign events + SyncData persistence.
///
/// State (the conversion records) lives in <see cref="ICultureConversionStore"/> — a singleton shared
/// with the service and the recruitment context adapter. SyncData round-trips it as a
/// <c>Dictionary&lt;string,string&gt;</c> (mirrors <c>MessengerCampaignBehavior</c>), including the
/// new-campaign reset guard via <c>_justLoadedFromSave</c>.
///
/// Entity State Matrix (re-apply on load mutates Settlement.Culture, an idempotent field write — not a
/// destructive Hero move — so it is safe across all settlement states; only converted records are touched).
/// </summary>
public class CultureConversionBehavior : CampaignBehaviorBase
{
    private const string SaveKey = "_taom_cultureconversion";

    private readonly ICultureConversionService _service;
    private readonly ICultureConversionStore _store;
    private readonly IModLogger _logger;

    private CampaignGameStarter _lastSessionStarter;
    private bool _justLoadedFromSave;

    public CultureConversionBehavior(
        ICultureConversionService service,
        ICultureConversionStore store,
        IModLogger logger)
    {
        _service = service;
        _store = store;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
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

    private void OnSettlementOwnerChanged(
        Settlement settlement,
        bool openToClaim,
        Hero newOwner,
        Hero oldOwner,
        Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        // Towns/castles only — bound villages follow their parent (handled inside the service).
        if (settlement == null || !settlement.IsFortification)
            return;
        _service.OnSettlementConquered(settlement.StringId, CampaignTime.Now.ToDays);
    }

    private void OnDailyTick() => _service.RunDailyChecks(CampaignTime.Now.ToDays);

    private void OnGameLoaded(CampaignGameStarter starter) => _service.ReapplyConvertedCultures();

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        // A brand-new campaign starts with no conversions. SyncData(IsLoading) has NOT run here, so
        // _justLoadedFromSave is false and clearing is correct.
        if (!_justLoadedFromSave)
            _store.Clear();
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // Singleton behavior survives across campaigns within one process. On a genuinely new starter
        // clear the store — UNLESS SyncData just loaded a save (flag set in SyncData(IsLoading)).
        if (_lastSessionStarter != starter)
        {
            _lastSessionStarter = starter;
            if (!_justLoadedFromSave)
                _store.Clear();
        }

        // Clear unconditionally (same-process save→load→save→load reuses the starter on the 2nd load,
        // so gating this inside the starter check would leave the flag stuck-on — Messengers #123).
        _justLoadedFromSave = false;
    }
}
