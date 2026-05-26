using System.Reflection;
using HarmonyLib;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.FormationPresets.UI;

namespace TAOM.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Boundary-layer service that attaches the "OOBButtonsOverlay" GauntletLayer when the
/// OOB UI handler activates. v1.3.15 field name verified via ilspycmd: <c>_dataSource</c>
/// (not <c>_orderOfBattleVM</c>) and <c>_isActive</c>.
///
/// Sentinel-collision note (per harmony-patches.md): we hold static-ish state across frames
/// (<c>_wasActive</c>). The polled <c>_isActive</c> is <c>false</c> both BEFORE the handler
/// activates AND AFTER it deactivates. The transition we care about is <c>false→true</c>
/// (attach) and <c>true→false</c> (detach). This is a level-trigger, not a sentinel→terminal
/// collision: the false→true→false cycle is symmetric and the detach branch only fires when
/// the prior frame was true.
/// </summary>
public sealed class OOBOverlayService : IOOBOverlayService
{
    private readonly IModLogger _logger;
    private readonly IFormationPresetService _presetService;
    private readonly IOrderOfBattleVMTracker _vmTracker;
    private readonly ICompanionTacticsSettingsProvider _settings;

    private FieldInfo _isActiveField;
    private FieldInfo _dataSourceField;
    private bool _initialized;
    private bool _inertMode;

    private bool _wasActive;
    private GauntletLayer _layer;
    private MissionScreen _attachedScreen;
    private OOBButtonsVM _vm;

    public OOBOverlayService(
        IModLogger logger,
        IFormationPresetService presetService,
        IOrderOfBattleVMTracker vmTracker,
        ICompanionTacticsSettingsProvider settings)
    {
        _logger = logger;
        _presetService = presetService;
        _vmTracker = vmTracker;
        _settings = settings;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        _isActiveField = AccessTools.Field(typeof(MissionGauntletOrderOfBattleUIHandler), "_isActive");
        _dataSourceField = AccessTools.Field(typeof(MissionGauntletOrderOfBattleUIHandler), "_dataSource");
        if (_isActiveField == null || _dataSourceField == null)
        {
            _inertMode = true;
            _logger.LogWarning("[CompanionTactics] OOB UI handler reflection failed — overlay disabled");
        }
    }

    public void OnTick(MissionGauntletOrderOfBattleUIHandler handler)
    {
        EnsureInitialized();
        if (_inertMode || handler == null) return;
        if (!_settings.EnableFormationPresets)
        {
            // Toggle flipped to off mid-mission — guarantee detach.
            if (_wasActive) Detach();
            _wasActive = false;
            return;
        }

        bool isActive;
        try { isActive = (bool)_isActiveField.GetValue(handler); }
        catch { return; }

        if (isActive && !_wasActive) Attach(handler);
        else if (!isActive && _wasActive) Detach();
        _wasActive = isActive;
    }

    public void Detach()
    {
        if (_layer != null && _attachedScreen != null)
        {
            try
            {
                _layer.InputRestrictions.ResetInputRestrictions();
                _attachedScreen.RemoveLayer(_layer);
            }
            catch (System.Exception ex) { _logger.LogWarning($"[CompanionTactics] Detach layer failed: {ex.Message}"); }
        }
        _vm?.OnFinalize();
        _vm = null;
        _layer = null;
        _attachedScreen = null;
        _wasActive = false;
    }

    private void Attach(MissionGauntletOrderOfBattleUIHandler handler)
    {
        try
        {
            // MissionView.MissionScreen is a public property on the MissionView base.
            var missionScreen = (handler as MissionView)?.MissionScreen;
            if (missionScreen == null) return;

            _vm = new OOBButtonsVM(_presetService, _vmTracker, _logger);
            _layer = new GauntletLayer("GauntletLayer", 200, false);
            // Required: without InputRestrictions the layer renders but never registers with the
            // MissionScreen's input dispatcher — buttons paint, clicks pass through. Same bug
            // class as #202 (EquipPresets). MissionScreen overlays need this just like ScreenBase
            // overlays; the v1.3.15 input dispatcher does not distinguish the two.
            _layer.InputRestrictions.SetInputRestrictions();
            _layer.LoadMovie("OOBButtonsOverlay", _vm);
            missionScreen.AddLayer(_layer);
            _attachedScreen = missionScreen;
            if (_settings.FormationPresetsDebug)
                _logger.LogDebug("[FormationPresets] OOB overlay attached");
        }
        catch (System.Exception ex)
        {
            _logger.LogError($"[CompanionTactics] OOB overlay attach failed: {ex.Message}");
            _vm = null;
            _layer = null;
            _attachedScreen = null;
        }
    }
}
