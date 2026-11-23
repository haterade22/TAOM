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
        _visuals = visuals;
        _logger = logger;
        _menuController = new RefugeMenuController(refuges, wardens, settings, menus, encounters);
        _refuges.ResetForNewSession();
    }

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
        // Vanilla starts disbanding a refuge itself when the warden dies with no other hero in
        // the roster (KillCharacterAction.MakeDead -> DisbandPartyAction.StartDisband); the
        // service orphan-adopts the leaderless row. Listener ORDER CANNOT be relied on here:
        // MbEvent dispatches LIFO (head-insert), so this handler fires BEFORE vanilla's queue-add;
        // the queue is actually emptied by RefugeService.HourlyTick's idempotent re-cancel pass
        // (and the LoadFrom belt for saves written inside vanilla's 1-day wait).
        CampaignEvents.OnPartyDisbandStartedEvent.AddNonSerializedListener(this, OnPartyDisbandStarted);
        // Vanilla's peace-time prisoner release enumerates caravans, war parties, villages and
        // garrisons only; refuge-held hero prisoners need their own listener.
        CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
        // Best-effort session teardown; same reasoning as FieldCamp: entity handles from a dead
        // map scene must not survive into the next campaign.
        CampaignEvents.OnGameOverEvent.AddNonSerializedListener(this, OnGameOver);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Direction-split: the load-time transient wipe must not run on a save, and loading starts
        // from nulled locals so a missing key yields an empty book, never the previous session's.
        if (dataStore.IsLoading)
        {
            Dictionary<string, RefugeData> refuges = null;
            int counter = 0;
            dataStore.SyncData("_taomRefuges", ref refuges);
            dataStore.SyncData("_taomRefugeCounter", ref counter);
            _refuges.LoadFrom(refuges, counter);
        }
        else
        {
            _refuges.SaveInto(out Dictionary<string, RefugeData> refuges, out int counter);
            dataStore.SyncData("_taomRefuges", ref refuges);
            dataStore.SyncData("_taomRefugeCounter", ref counter);
        }
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // Menus register unconditionally; runtime gates live at the option conditions, so an MCM
        // toggle mid-session needs no relaunch (FiefHub lesson).
        _menuController.AddMenus(starter);
    }

    private void OnGameLoaded(CampaignGameStarter starter) => _refuges.OnGameLoaded();

    private void OnTick(float dt)
    {
        // UNCONDITIONAL, like FieldCampCampaignBehavior.OnTick: the frame work is mostly
        // state-protecting (build finish, post-load visual rebuild, cloth wind) and gating it on
        // the master toggle froze a mid-build refuge into an unreachable state with the garrison
        // inside. The one gameplay effect in there (the hold-nearby pin) gates on Enabled inside
        // the service.
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
        if (_refuges.AllRefuges.Count == 0)
            return;
        foreach (var refugeId in RefugePartyIds(mapEvent))
            _refuges.OnMapEventStarted(refugeId);
    }

    private void OnMapEventEnded(MapEvent mapEvent)
    {
        if (_refuges.AllRefuges.Count == 0)
            return;
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

    private void OnPartyDisbandStarted(MobileParty party)
    {
        if (party?.PartyComponent is RefugePartyComponent && !string.IsNullOrEmpty(party.StringId))
            _refuges.OnPartyDisbandStarted(party.StringId);
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
