using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// The Fords of War command panel layer: attaches <c>FordsCommand.xml</c> once the session is active, refreshes the view model four
/// times a second and reads the hotkeys (1 to 4 lane, F1 to F8 buy a pack, H field the first affordable hero, Z Push, X Hold, C Fall
/// back). The layer shows the cursor for the whole mission and takes mouse buttons and the wheel, but only where a widget takes
/// events; the camera view reads keys, the right button and the wheel off the scene. A cursor left hidden is pinned to the screen
/// centre and a layer under it gets no clicks (#970), so the restriction is set before the movie loads. Built by the mission's
/// <c>[ViewMethod]</c> list (<see cref="FordsOfWarMissionViews"/>). Any fault latches the panel off and logs once. The panel only
/// offers the result button; the automatic return to the menu belongs to the mission logic (<c>FordsMissionExit</c>).
/// </summary>
public sealed class FordsCommandView : MissionView
{
    // Above the vanilla mission HUD layers (orders, agent status and kill feed use 13 to 17), below the options handler (49) and the
    // escape menu (50): the panel draws under the paused menu and the menu, a focus layer over the whole screen, takes the mouse first.
    internal const int LayerOrder = 20;

    private const float RefreshIntervalSec = 0.25f;

    private FordsSession? _session;
    private GauntletLayer? _layer;
    private GauntletMovieIdentifier? _movie;
    private FordsCommandVM? _vm;
    private FordsRunReport? _report;
    private float _sinceRefresh = RefreshIntervalSec;
    private bool _failed;

    public override void OnMissionScreenTick(float dt)
    {
        base.OnMissionScreenTick(dt);
        if (_failed || Mission == null)
            return;

        try
        {
            _session ??= IoC.Resolve<FordsSession>();
            if (_session == null)
                return;

            if (_vm == null && _session.IsActive)
                Attach();

            if (_vm == null)
                return;

            _vm.Tick(dt, MBCommon.IsPaused);
            _report?.Paused(MBCommon.IsPaused);
            if (!_vm.IsResultVisible)
                FordsCommandHotkeys.Read(Input, _vm);

            _sinceRefresh += dt;
            if (_sinceRefresh >= RefreshIntervalSec)
            {
                _sinceRefresh = 0f;
                _vm.Refresh();
            }
        }
        catch (Exception ex)
        {
            _failed = true;
            IoC.Resolve<IModLogger>()?.LogError($"[FordsOfWar] command panel failed, layer removed: {ex}");
            TryDetach();
        }
    }

    public override void OnMissionScreenFinalize()
    {
        TryDetach();
        base.OnMissionScreenFinalize();
    }

    private void Attach()
    {
        _report = IoC.Resolve<FordsRunReport>();
        _vm = new FordsCommandVM(_session!, ExitClicked, _report);
        _layer = new GauntletLayer("GauntletLayer", LayerOrder, false);
        _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.Mouse);
        _movie = _layer.LoadMovie("FordsCommand", _vm);
        MissionScreen.AddLayer(_layer);
        _vm.Refresh();
        _report?.PanelAttached();
    }

    private void ExitClicked()
    {
        _report?.EndGameRequested("button");
        FordsOfWarGameManager.ReturnToMainMenu(IoC.Resolve<IModLogger>());
    }

    // Detach with no way to throw out of a screen callback; a failure here is only logged.
    private void TryDetach()
    {
        try
        {
            if (_layer != null)
            {
                if (_movie != null)
                    _layer.ReleaseMovie(_movie);
                _layer.InputRestrictions.ResetInputRestrictions();
                MissionScreen?.RemoveLayer(_layer);
            }

            _vm?.OnFinalize();
        }
        catch (Exception ex)
        {
            IoC.Resolve<IModLogger>()?.LogError($"[FordsOfWar] command panel detach failed: {ex.Message}");
        }

        _vm = null;
        _layer = null;
        _movie = null;
    }
}
