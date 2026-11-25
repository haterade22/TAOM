using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FieldCamp.Domain;
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

    /// <summary>
    /// Attaches the overlay map view (ADR-013). TAOM.Client's adapter replaces core's null one when
    /// FieldCampIoC.InitializePatchStatics runs, after the client composition. A static rather than a constructor
    /// parameter because SubModule.OnGameStart builds this behavior with new (a fresh one per campaign, for the
    /// session reset) and that file is single-owner.
    /// </summary>
    internal static IFieldCampOverlayAdapter Overlay { get; set; } = new NullFieldCampOverlayAdapter();

    private readonly ICampService _camps;
    private readonly ICampVisualService _visuals;
    private readonly IModLogger _logger;
    private readonly FieldCampMenuController _menuController;

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
        _camps.ResetForNewSession();
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
        if (dataStore.IsLoading)
        {
            // Null-first: BehaviorSaveData.SyncData returns false and leaves the ref UNCHANGED
            // when the key is missing (CampaignBehaviorDataStore, 1.4.8), so pre-seeding from the
            // live singleton would silently keep the previous session's book on a key miss.
            // Null makes a missing key load as an empty book instead (round-B / Codex round-2 #2).
            Dictionary<string, CampState> camps = null;
            dataStore.SyncData("_taomFieldCamps", ref camps);
            _camps.LoadFrom(camps);
        }
        else
        {
            // Save direction never calls LoadFrom: LoadFrom carries load-only semantics
            // (ResetTransientState wipes live inquiry latches and the ambush-scan clock), so
            // running it on every autosave mutates live state (round-B, SupplyLines is the shape).
            _camps.SaveInto(out Dictionary<string, CampState> camps);
            dataStore.SyncData("_taomFieldCamps", ref camps);
        }
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // Menus register unconditionally; runtime gates live at the option conditions and the
        // overlay button, so an MCM toggle mid-session needs no relaunch (FiefHub lesson).
        _menuController.AddMenus(starter);
    }

    private void OnGameLoaded(CampaignGameStarter starter) => _camps.OnGameLoaded();

    private void OnHourlyTick()
    {
        // Unconditional fan-out: the service gates gameplay effects on the toggle itself but keeps
        // the state-protecting paths (settlement fold, captivity break, move guard) running, so a
        // disabled feature can still clean up a standing camp.
        _camps.HourlyTick();
    }

    internal void OnTick(float dt)
    {
        // The overlay is (re)attached from the tick, not from session launch: MapScreen.Instance
        // does not exist yet when OnSessionLaunched fires, and the screen is rebuilt on every
        // save-load. GetMapView is a short list scan, cheap at frame rate.
        Overlay.EnsureAttached();

        _camps.FrameTick();
    }

    private void OnGameOver()
    {
        _visuals.ClearAll();
        PartyNameplateCampIconPatch.Reset();
        _logger.LogInfo("[FieldCamp] game over - camp visuals and nameplate icon state cleared");
    }
}
