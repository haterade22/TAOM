using System.Collections.Generic;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FieldCamp.Domain;
using TAOM.Features.FieldCamp.UI;
using TAOM.Features.SupplyLines;

namespace TAOM.Features.FieldCamp.Hooks;

/// <summary>
/// Campaign entry point for FieldCamp (#506): registers the menus, keeps the map overlay view
/// alive, fans the campaign ticks into <see cref="ICampService"/>, and owns the SyncData halves.
/// All decisions live in the service; menu glue lives in <see cref="FieldCampMenuController"/>
/// (ADR-002 thin entry point).
/// </summary>
public class FieldCampCampaignBehavior : CampaignBehaviorBase
{
    public const string BaseMenuId = "taom_field_camp_menu";
    public const string CampSubMenuId = "taom_fc_camp";

    private readonly ICampService _camps;
    private readonly ICampVisualService _visuals;
    private readonly IModLogger _logger;
    private readonly FieldCampMenuController _menuController;

    /// <summary>True only when SyncData ran in the LOADING direction this session. OnSessionLaunched
    /// consults it FIRST: false means a fresh campaign OR a save without our record, and either way
    /// the process-lifetime service must not keep the previous session's book (round-A CRITICAL).</summary>
    private bool _syncedThisSession;

    public FieldCampCampaignBehavior(
        ICampService camps,
        ICampSettingsProvider settings,
        ICampVisualService visuals,
        ISupplyLinesSettingsProvider supplySettings,
        IGameMenuAdapter menus,
        IModLogger logger)
    {
        _camps = camps;
        _visuals = visuals;
        _logger = logger;
        _menuController = new FieldCampMenuController(camps, settings, supplySettings, menus);
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        // Best-effort session teardown. There is no campaign-end event on CampaignBehaviorBase;
        // the reliable teardown path is FieldCampMapView.OnFinalize (the MapScreen dies with the
        // campaign), and this covers the game-over flow where the screen may outlive the state.
        CampaignEvents.OnGameOverEvent.AddNonSerializedListener(this, OnGameOver);
    }

    public override void SyncData(IDataStore dataStore)
    {
        _camps.SaveInto(out Dictionary<string, CampState> camps);
        dataStore.SyncData("_taomFieldCamps", ref camps);
        _camps.LoadFrom(camps);
        if (dataStore.IsLoading)
            _syncedThisSession = true;
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // FIRST: reset before anything can read or save a stale book.
        ResetIfNoLoadedRecord();

        // Menus register unconditionally; runtime gates live at the option conditions and the
        // overlay button, so an MCM toggle mid-session needs no relaunch (FiefHub lesson).
        _menuController.AddMenus(starter);
    }

    /// <summary>
    /// True when this session had no loading SyncData (fresh campaign, or a save from before the
    /// feature): whatever book the process-lifetime singleton holds belongs to a PREVIOUS session
    /// and has been reset. Internal for TAOM.Tests (InternalsVisibleTo); idempotent, so both
    /// OnGameLoaded and OnSessionLaunched may call it.
    /// </summary>
    internal bool ResetIfNoLoadedRecord()
    {
        if (_syncedThisSession)
            return false;
        _camps.ResetForNewSession();
        return true;
    }

    private void OnGameLoaded(CampaignGameStarter starter)
    {
        // A save from before the feature has no record, so SyncData never ran and the book still
        // belongs to the previous session; reset instead of re-showing its stale visuals.
        // (OnSessionLaunched re-checks, covering fresh campaigns where this event never fires.)
        if (ResetIfNoLoadedRecord())
            return;
        _camps.OnGameLoaded();
    }

    private void OnHourlyTick()
    {
        // Unconditional fan-out: the service gates gameplay effects on the toggle itself but keeps
        // the state-protecting paths (settlement fold, captivity break, move guard) running, so a
        // disabled feature can still clean up a standing camp.
        _camps.HourlyTick();
    }

    private void OnTick(float dt)
    {
        // The overlay is (re)attached from the tick, not from session launch: MapScreen.Instance
        // does not exist yet when OnSessionLaunched fires, and the screen is rebuilt on every
        // save-load. GetMapView is a short list scan, cheap at frame rate.
        EnsureOverlay();

        _camps.FrameTick();
    }

    private void OnGameOver()
    {
        _visuals.ClearAll();
        PartyNameplateCampIconPatch.Reset();
        _logger.LogInfo("[FieldCamp] game over - camp visuals and nameplate icon state cleared");
    }

    private static void EnsureOverlay()
    {
        var mapScreen = MapScreen.Instance;
        if (mapScreen == null)
            return;
        if (mapScreen.GetMapView<FieldCampMapView>() != null)
            return;

        mapScreen.AddMapView<FieldCampMapView>();
    }
}
