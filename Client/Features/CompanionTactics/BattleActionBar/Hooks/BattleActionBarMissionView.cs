using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.BattleActionBar.UI;

namespace TAOM.Features.CompanionTactics.BattleActionBar.Hooks;

/// <summary>
/// MissionView that owns the BattleActionBar GauntletLayer lifecycle. Built by the engine as a
/// <c>[DefaultView]</c> in every mission with a screen (MissionScreen.OnMissionAfterStarting registers it), because a
/// view TAOM adds in OnMissionBehaviorInitialize is never registered and never ticks (#970). Attaches in field
/// battles (<c>Mission.IsFieldBattle</c>) on the first screen tick once the mode is Battle: the mode is Deployment at
/// screen init (DeploymentHandler.AfterStart). The layer takes mouse buttons and nothing else, so the cursor stays
/// hidden and attack, block and the camera keep working. Parked (#970): with the cursor hidden the engine pins it to
/// the screen centre, so a button can be clicked only while another view shows a cursor (the Esc menu); the stances
/// change nothing in battle, so the setting is off by default. Refreshes the action bar VM at most twice per second
/// based on the player's selected formation. Turning the setting off mid-battle removes the layer.
/// </summary>
[DefaultView]
public sealed class BattleActionBarMissionView : MissionView
{
    private const float RefreshIntervalSec = 0.5f;

    // The [Check] key (#1072): counts every mission screen of the process. Main thread only.
    private static int _missionSerial;

    private ICompanionTacticsSettingsProvider? _settings;
    private IBattleActionBarService? _service;
    private ITroopStanceManager? _stances;
    private IModLogger? _logger;
    private IOutcomeLog? _outcomes;

    // Per mission (the engine builds a new view for each): the attach check's verdict at OnMissionScreenFinalize.
    private string _missionKey = "";
    private bool _fieldBattle;
    private bool _attachedOnce;
    private bool _enabledBattleTick;

    private GauntletLayer? _layer;
    private GauntletMovieIdentifier? _movie;
    private BattleActionBarVM? _vm;
    private bool _isInitialized;
    private bool _attachAttempted;
    private float _refreshAccumulator;

    public override void OnMissionScreenInitialize()
    {
        base.OnMissionScreenInitialize();
        _settings = IoC.Resolve<ICompanionTacticsSettingsProvider>();
        _service = IoC.Resolve<IBattleActionBarService>();
        _stances = IoC.Resolve<ITroopStanceManager>();
        _logger = IoC.Resolve<IModLogger>();
        _outcomes = IoC.Resolve<IOutcomeLog>();
        _missionKey = (++_missionSerial).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _fieldBattle = Mission.IsFieldBattle;

        if (Mission.IsFieldBattle)
        {
            _logger?.LogInfo($"[BattleActionBar] screen init: MissionScreen={(MissionScreen != null ? "set" : "NULL")} mode={Mission.Mode} enabled={_settings?.ShowBattleActionBar}");
        }
    }

    private void TryAttachLayer()
    {
        if (_service == null || _stances == null) return;

        try
        {
            // A mission that ended without EndMission leaves its stances behind.
            _stances.ClearAllStances();
            _vm = new BattleActionBarVM(_service, _stances);
            _layer = new GauntletLayer("GauntletLayer", 100, false);
            // Mouse buttons only. The parameterless restriction call shows the
            // cursor and takes all input, which stops MissionMainAgentController's attack and block (it runs
            // only while the mouse is hidden) and turns the camera into an edge scroll. Without any restriction
            // the layer never registers with the MissionScreen's input dispatcher and clicks fall through.
            _layer.InputRestrictions.SetInputRestrictions(false, InputUsageMask.MouseButtons);
            _movie = _layer.LoadMovie("BattleActionBar", _vm);
            MissionScreen.AddLayer(_layer);
            _isInitialized = true;
            _logger?.LogInfo("[BattleActionBar] mission init — layer attached");
            _attachedOnce = true;
            // LoadMovie does not throw for a missing prefab (WidgetFactory.GetCustomType returns null), so check it loaded.
            if (_movie.Movie?.IsLoaded == true)
                _outcomes?.Pass(BattleActionBarChecks.AttachId, _missionKey, "mission=" + _missionKey);
            else
                _outcomes?.Fail(BattleActionBarChecks.AttachId, _missionKey, "reason=movie-not-loaded mission=" + _missionKey);
        }
        catch (System.Exception ex)
        {
            _logger?.LogError($"[BattleActionBar] layer attach failed: {ex.Message}");
            _outcomes?.Fail(BattleActionBarChecks.AttachId, _missionKey,
                "reason=exception mission=" + _missionKey + " type=" + ex.GetType().Name + " message=" + ex.Message);
        }
    }

    public override void OnMissionScreenTick(float dt)
    {
        base.OnMissionScreenTick(dt);
        if (_settings == null) return;
        if (!_settings.ShowBattleActionBar)
        {
            // Turned off mid-battle: hide the bar, drop the layer and its input restrictions. Turning it back
            // on attaches once more (the attach guard is _isInitialized, so a layer is never added twice).
            if (_isInitialized) DetachLayer();
            _attachAttempted = false;
            return;
        }
        if (!_isInitialized)
        {
            if (Mission.Mode == MissionMode.Battle) _enabledBattleTick = true;
            // Mode is Deployment at screen init in a field battle (DeploymentHandler.cs:41): attach on the first Battle tick.
            if (_attachAttempted || !ShouldAttach(true, Mission.IsFieldBattle, Mission.Mode)) return;
            _attachAttempted = true;
            TryAttachLayer();
            if (!_isInitialized) return;
        }
        if (_vm == null) return;

        _refreshAccumulator += dt;
        if (_refreshAccumulator >= RefreshIntervalSec)
        {
            _refreshAccumulator = 0f;
            RefreshFromSelectedFormation();
        }
    }

    public override void OnMissionScreenFinalize()
    {
        // MissionScreen calls this from Mission.EndMissionInternal before the mission is over, so its mode is still set.
        var end = BattleActionBarChecks.ClassifyAtEnd(_fieldBattle, _attachedOnce, _enabledBattleTick,
            endedInBattle: Mission?.Mode == MissionMode.Battle, enabledAtEnd: _settings?.ShowBattleActionBar == true);
        if (end == BattleActionBarChecks.AttachEnd.FailNeverAttached)
            _outcomes?.Fail(BattleActionBarChecks.AttachId, _missionKey, "reason=never-attached mission=" + _missionKey);
        else if (end == BattleActionBarChecks.AttachEnd.SkipSettingOff)
            _outcomes?.Skip(BattleActionBarChecks.AttachId, _missionKey, "reason=setting-off mission=" + _missionKey);

        if (_isInitialized)
        {
            _stances?.ClearAllStances();
            DetachLayer();
        }
        base.OnMissionScreenFinalize();
    }

    private void DetachLayer()
    {
        try
        {
            if (_vm != null) _vm.IsVisible = false;
            if (_layer != null)
            {
                if (_movie != null) _layer.ReleaseMovie(_movie);
                _layer.InputRestrictions.ResetInputRestrictions();
                MissionScreen?.RemoveLayer(_layer);
            }
        }
        catch (System.Exception ex) { _logger?.LogWarning($"[BattleActionBar] cleanup: {ex.Message}"); }
        _vm?.OnFinalize();
        _vm = null;
        _layer = null;
        _movie = null;
        _isInitialized = false;
    }

    private void RefreshFromSelectedFormation()
    {
        var team = Mission.Current?.PlayerTeam;
        var orderController = team?.PlayerOrderController;
        var selected = orderController?.SelectedFormations;
        var formation = (selected != null && selected.Count > 0) ? selected[0] : null;

        // P3-1 fix (Codex review #36, 2026-05-06): the original code short-circuited when
        // the formation matched the previous one, which prevented the bar from picking up external
        // stance changes (Patch35_Formation_SetMovementOrder cleared the stance but the
        // visible button stayed highlighted). Lift the short-circuit; on same-formation
        // refresh we still rebuild buttons + re-sync IsActive from the stance manager,
        // which is cheap (≤9 buttons) and runs at most twice per second.
        var adapter = formation != null ? new FormationAdapter(formation) : null;
        _vm!.UpdateForFormation(formation, adapter);
    }

    internal static bool ShouldAttach(bool enabled, bool isFieldBattle, MissionMode mode) =>
        enabled && isFieldBattle && mode == MissionMode.Battle;
}
