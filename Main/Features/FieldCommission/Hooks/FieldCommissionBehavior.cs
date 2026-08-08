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
/// foreign world event never resets or completes our tracking. Persistence lives in
/// <see cref="FieldCommissionSaveData"/>, session teardown in <see cref="FieldCommissionSessionReset"/>.
/// </summary>
public class FieldCommissionBehavior : CampaignBehaviorBase
{
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
            FieldCommissionSaveData.Save(dataStore, _merit);
            return;
        }

        FieldCommissionSaveData.Load(dataStore, _merit);
        _justLoadedFromSave = true; // tells OnSessionLaunched NOT to clear the freshly-loaded state
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

        // The master toggle is re-read at the CLOSE of the window, not only at its start: the player
        // can reach Mod Options mid-battle, and BeginBattle already latched `_eligible`. Folded into
        // `won` it takes the score-nothing path — no merit, no offer — while the `finally` still
        // clears the window.
        var won = _configProvider.GetConfig().Enabled && mapEvent.WinningSide == mapEvent.PlayerSide;
        _merit.EndBattle(won);
    }

    private void OnTick(float dt)
    {
        if (!_coopSession.IsAuthority || _enlistment.IsEnlisted || !_configProvider.GetConfig().Enabled)
            return;
        if (PlayerEncounter.Current != null || MapEvent.PlayerMapEvent != null)
            return;

        // Captivity clears PlayerEncounter.Current, so the gates above go quiet exactly when the
        // player can least act on a promotion — in a cell, with no roster to promote from.
        if (Hero.MainHero == null || Hero.MainHero.IsPrisoner || MobileParty.MainParty == null)
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

            // Unconditional, ahead of the load guard: the persisted bank must survive a load, but the
            // un-persisted offer queue and latch must not survive ANY session boundary. Gating this
            // on `!_justLoadedFromSave` is what would let a previous campaign's offers through.
            FieldCommissionSessionReset.ClearCarriedOverOffers(_merit, _offerFlow);

            if (!_justLoadedFromSave)
                ClearState();
        }

        _justLoadedFromSave = false;
    }

    private void ClearState() => FieldCommissionSessionReset.ClearAll(_merit, _offerFlow);
}
