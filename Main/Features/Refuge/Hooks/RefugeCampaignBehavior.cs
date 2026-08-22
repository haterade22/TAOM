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

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        // Best-effort session teardown; same reasoning as FieldCamp: entity handles from a dead
        // map scene must not survive into the next campaign.
        CampaignEvents.OnGameOverEvent.AddNonSerializedListener(this, OnGameOver);
    }

    public override void SyncData(IDataStore dataStore)
    {
        _refuges.SaveInto(out Dictionary<string, RefugeData> refuges, out int counter);
        dataStore.SyncData("_taomRefuges", ref refuges);
        dataStore.SyncData("_taomRefugeCounter", ref counter);
        _refuges.LoadFrom(refuges, counter);
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // Menus register unconditionally; runtime gates live at the option conditions, so an MCM
        // toggle mid-session needs no relaunch (FiefHub lesson).
        _menuController.AddMenus(starter);
    }

    private void OnGameLoaded(CampaignGameStarter starter)
    {
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
