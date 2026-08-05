using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;

namespace TAOM.Features.FieldCommission.Hooks;

/// <summary>
/// Thin event router (ADR-002) for Battlefield Promotions — converts sealed TaleWorlds types to
/// primitives at the boundary; all decisions live in the merit/offer services. Bug fix (c): gates
/// <c>IsPlayerMapEvent</c> FIRST and tracks the SPECIFIC <see cref="MapEvent"/> instance, so a
/// foreign world event elsewhere never resets or completes our tracking.
/// </summary>
public class FieldCommissionBehavior : CampaignBehaviorBase
{
    private const string MeritsKey = "_taom_fc_merits";
    private const string PromotedKey = "_taom_fc_promotedHeroes";

    private readonly IFieldCommissionMeritService _merit;
    private readonly IFieldCommissionOfferFlowService _offerFlow;
    private readonly IFieldCommissionConfigProvider _configProvider;
    private readonly IEnlistmentStateQuery _enlistment;
    private readonly ICoopSessionProvider _coopSession;
    private readonly IHeroCommissionAdapter _heroCommission;

    private MapEvent _trackedMapEvent;
    private CampaignGameStarter _lastSessionStarter;
    private bool _justLoadedFromSave;

    public FieldCommissionBehavior(
        IFieldCommissionMeritService merit,
        IFieldCommissionOfferFlowService offerFlow,
        IFieldCommissionConfigProvider configProvider,
        IEnlistmentStateQuery enlistment,
        ICoopSessionProvider coopSession,
        IHeroCommissionAdapter heroCommission)
    {
        _merit = merit;
        _offerFlow = offerFlow;
        _configProvider = configProvider;
        _enlistment = enlistment;
        _coopSession = coopSession;
        _heroCommission = heroCommission;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
        {
            var merits = _merit.ExportMerits();
            var promoted = _merit.ExportPromotedHeroIds();
            dataStore.SyncData(MeritsKey, ref merits);
            dataStore.SyncData(PromotedKey, ref promoted);
        }
        else
        {
            Dictionary<string, int> merits = null;
            List<string> promoted = null;
            dataStore.SyncData(MeritsKey, ref merits);
            dataStore.SyncData(PromotedKey, ref promoted);
            _merit.ImportMerits(merits);
            _merit.ImportPromotedHeroIds(promoted);
            _justLoadedFromSave = true; // tells OnSessionLaunched NOT to clear the freshly-loaded state
        }
    }

    private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        if (mapEvent == null || !mapEvent.IsPlayerMapEvent || !_coopSession.IsAuthority || !_configProvider.GetConfig().Enabled)
            return;

        _trackedMapEvent = mapEvent;
        if (_enlistment.IsEnlisted) // forced ineligible — own-party health isn't a fair-fight signal while enlisted
        {
            _merit.BeginBattle(false);
            return;
        }

        var playerHealthy = MobileParty.MainParty?.MemberRoster?.TotalHealthyCount ?? 0;
        var enemyHealthy = MapEventSideHelper.GetEnemySide(mapEvent)?.GetTotalHealthyTroopCountOfSide() ?? 0;
        var ratio = _merit.ComputeRatio(playerHealthy, enemyHealthy);
        var eligible = playerHealthy > 0 && _merit.IsBattleEligible(ratio, _configProvider.GetConfig().RatioThreshold);
        _merit.BeginBattle(eligible);
    }

    private void OnMapEventEnded(MapEvent mapEvent)
    {
        if (mapEvent == null || !mapEvent.IsPlayerMapEvent || !ReferenceEquals(mapEvent, _trackedMapEvent))
            return;

        _trackedMapEvent = null;
        if (!_coopSession.IsAuthority)
            return;
        _merit.EndBattle(mapEvent.WinningSide == mapEvent.PlayerSide);
    }

    private void OnTick(float dt)
    {
        if (!_coopSession.IsAuthority || _enlistment.IsEnlisted || !_configProvider.GetConfig().Enabled)
            return;
        if (PlayerEncounter.Current != null || MapEvent.PlayerMapEvent != null)
            return;
        _offerFlow.PumpNextOffer();
    }

    private void OnGameLoaded(CampaignGameStarter starter)
    {
        if (_coopSession.IsAuthority)
            _merit.PruneDeadPromotedHeroes(_heroCommission.IsHeroAliveAndValid);
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        if (!_justLoadedFromSave)
            ClearState();
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        if (_lastSessionStarter != starter)
        {
            _lastSessionStarter = starter;
            if (!_justLoadedFromSave)
                ClearState();
        }

        _justLoadedFromSave = false;
    }

    private void ClearState()
    {
        _merit.ImportMerits(null);
        _merit.ImportPromotedHeroIds(null);
    }
}
