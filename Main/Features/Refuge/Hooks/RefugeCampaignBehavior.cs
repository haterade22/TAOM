using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Refuge.Components;
using TAOM.Features.Refuge.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.Refuge.Hooks;

/// <summary>
/// Campaign entry point for Refuge: registers the menus, fans the campaign ticks into
/// <see cref="IRefugeService"/>, maps map-event parties to refuge ids, and owns the SyncData
/// halves. All decisions live in the service; menu glue lives in
/// <see cref="RefugeMenuController"/> (ADR-002 thin entry point, FieldCampCampaignBehavior shape).
/// </summary>
public class RefugeCampaignBehavior : CampaignBehaviorBase
{
    public const string MenuId = "taom_refuge_menu";

    private readonly IRefugeService _refuges;
    private readonly IRefugeSettingsProvider _settings;
    private readonly IRefugeVisualService _visuals;
    private readonly IModLogger _logger;
    private readonly RefugeMenuController _menuController;

    public RefugeCampaignBehavior(
        IRefugeService refuges,
        IRefugeSettingsProvider settings,
        IRefugeVisualService visuals,
        IWardenService wardens,
        IGameMenuAdapter menus,
        IEncounterAdapter encounters,
        IModLogger logger)
    {
        _refuges = refuges;
        _settings = settings;
        _visuals = visuals;
        _logger = logger;
        _menuController = new RefugeMenuController(refuges, wardens, settings, menus, encounters);
    }

    /// <summary>True once SyncData ran in LOADING mode this session: the book was rebuilt from a
    /// real save record. False for a fresh campaign AND for a save written before this feature
    /// existed - both must start from an empty book, not the previous session's singleton state.</summary>
    private bool _syncedThisSession;

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        // A lost defense wipes the garrison and the engine applies DestroyPartyAction directly
        // from MapEventSide.HandleMapEventEnd, AFTER OnMapEventEnded already dispatched; without
        // this listener the book row, cap slot and visuals leak until the next load's reconcile.
        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
        // Vanilla's peace-time prisoner release enumerates caravans, war parties, villages and
        // garrisons only; refuge-held hero prisoners need their own listener.
        CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
        // Best-effort session teardown; same reasoning as FieldCamp: entity handles from a dead
        // map scene must not survive into the next campaign.
        CampaignEvents.OnGameOverEvent.AddNonSerializedListener(this, OnGameOver);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsLoading)
            _syncedThisSession = true;
        _refuges.SaveInto(out Dictionary<string, RefugeData> refuges, out int counter);
        dataStore.SyncData("_taomRefuges", ref refuges);
        dataStore.SyncData("_taomRefugeCounter", ref counter);
        _refuges.LoadFrom(refuges, counter);
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // FIRST: the session-reset gate, before any menu or tick can read the book.
        ResetIfNoLoadedRecord();
        // Menus register unconditionally; runtime gates live at the option conditions, so an MCM
        // toggle mid-session needs no relaunch (FiefHub lesson).
        _menuController.AddMenus(starter);
    }

    /// <summary>The reset half of the singleton-leak fix (the FieldCamp/SupplyLines shape): when
    /// no save record loaded this session (fresh campaign, or a pre-feature save), the
    /// process-lifetime service still holds the PREVIOUS campaign's book and would save it into
    /// this one. Latches afterwards so the OnGameLoaded and OnSessionLaunched callers cannot
    /// double-reset (a second wipe would eat anything founded right after launch). Internal for
    /// direct test access (OnSessionLaunched also needs a real CampaignGameStarter).</summary>
    internal bool ResetIfNoLoadedRecord()
    {
        if (_syncedThisSession)
            return false;
        _refuges.ResetForNewSession();
        _syncedThisSession = true;
        _logger.LogInfo("[Refuge] no saved record this session - book and transients reset.");
        return true;
    }

    private void OnGameLoaded(CampaignGameStarter starter)
    {
        // A save from before the feature has no record: SyncData never ran and the singleton
        // book still belongs to the previous session, so reset instead of reconciling (and
        // re-showing) its stale state. (OnSessionLaunched re-checks for fresh campaigns.)
        if (ResetIfNoLoadedRecord())
            return;
        _refuges.OnGameLoaded();
    }

    private void OnTick(float dt)
    {
        // Cheap gate: the whole per-frame body (build advancement, hold-nearby rule, visual
        // retries) hangs off the master toggle. Build progress is wall-clock-derived from
        // BuildStartTime, so a mid-build toggle-off only pauses the FINISH transition, never the
        // clock; re-enabling completes the build on the next frame.
        if (_settings.Enabled)
            _refuges.FrameTick();
    }

    private void OnHourlyTick()
    {
        // No Enabled gate here on purpose: HourlyTick's only job is the raid roll, which has its
        // own EnableRaids setting inside the service. Standing refuges keep existing (and keep
        // their own policies) with the master toggle off; the toggle only stops founding + menus.
        _refuges.HourlyTick();
    }

    private void OnMapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender)
    {
        foreach (var refugeId in RefugePartyIds(mapEvent))
            _refuges.OnMapEventStarted(refugeId);
    }

    private void OnMapEventEnded(MapEvent mapEvent)
    {
        foreach (var refugeId in RefugePartyIds(mapEvent))
            _refuges.OnMapEventEnded(refugeId);
        // A defeated refuge is destroyed AFTER this callback (MapEvent.FinalizeEventAux runs
        // MapEventSide.HandleMapEventEnd next); OnMobilePartyDestroyed is the reconcile seam.
    }

    private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyer)
    {
        if (party?.PartyComponent is RefugePartyComponent && !string.IsNullOrEmpty(party.StringId))
            _refuges.OnPartyDestroyed(party.StringId);
    }

    private void OnMakePeace(
        IFaction side1, IFaction side2, TaleWorlds.CampaignSystem.Actions.MakePeaceAction.MakePeaceDetail detail)
    {
        // Only a peace touching the player's own faction can change a refuge prisoner's
        // eligibility (refuge parties are player-clan); the service re-checks per prisoner.
        var playerFaction = Clan.PlayerClan?.MapFaction;
        if (playerFaction == null || side1 == playerFaction || side2 == playerFaction)
            _refuges.OnPeaceMade();
    }

    /// <summary>Maps a map event to the refuge parties in it. Materialized inside the guard:
    /// InvolvedParties walks live battle sides, and one throwing party must not turn a militia
    /// stand-down into a permanently-baked garrison (the exact defect the persisted militia
    /// bookkeeping exists to prevent).</summary>
    private static List<string> RefugePartyIds(MapEvent mapEvent)
    {
        var ids = new List<string>();
        if (mapEvent == null)
            return ids;
        try
        {
            foreach (var party in mapEvent.InvolvedParties)
            {
                var mobile = party?.MobileParty;
                if (mobile?.PartyComponent is RefugePartyComponent && !string.IsNullOrEmpty(mobile.StringId))
                    ids.Add(mobile.StringId);
            }
        }
        catch
        {
            // A partially-torn-down event yields whatever ids were collected before the throw.
        }
        return ids;
    }

    private void OnGameOver()
    {
        _visuals.ClearAll();
        _logger.LogInfo("[Refuge] game over - refuge visuals cleared");
    }
}
