using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ScreenSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege.UI;

namespace TAOM.Features.StagedSiege.Hooks;

/// <summary>
/// The staged siege HUD layer. Built by the engine as a <c>[DefaultView]</c> in every mission with a screen (a view TAOM adds
/// itself is never registered, #970); inert unless a staged siege or a staged hall is armed for the mission. Each screen tick reads
/// <see cref="StagedSiegeSlot.SnapshotFor"/> or, in the lord's hall, <see cref="StagedSiegeSlot.HallFor"/> (both null when nothing is
/// armed for this mission, which detaches the layer), refreshes the text four times a second and projects the marker over the
/// current point every frame. The layer takes no input
/// restrictions and every widget ignores events, so attack, block and the camera are untouched. The core assembly's internals
/// are visible to TAOM.Client, so no seam interface sits between the slot and this view.
/// </summary>
[DefaultView]
public sealed class StagedSiegeMissionView : MissionView
{
    private const float RefreshIntervalSec = 0.25f;
    private const float MarkerHeightMetres = 3f;

    private GauntletLayer? _layer;
    private GauntletMovieIdentifier? _movie;
    private StagedSiegeHudVM? _vm;
    private float _sinceRefresh = RefreshIntervalSec;
    private bool _failed;

    public override void OnMissionScreenTick(float dt)
    {
        base.OnMissionScreenTick(dt);
        // Latched: a repeating fault would otherwise log and rebuild the layer every frame.
        if (_failed || Mission == null)
            return;

        try
        {
            var snapshot = StagedSiegeSlot.SnapshotFor(Mission);
            var hall = snapshot == null ? StagedSiegeSlot.HallFor(Mission) : null;
            if (snapshot == null && hall == null)
            {
                Detach();
                return;
            }

            if (_layer == null)
                Attach();
            if (_vm == null)
                return;

            _sinceRefresh += dt;
            if (_sinceRefresh >= RefreshIntervalSec)
            {
                _sinceRefresh = 0f;
                if (snapshot != null)
                    _vm.Refresh(snapshot);
                else
                    _vm.Refresh(hall!);
            }

            if (snapshot != null)
                PlaceMarker(snapshot.Finished, snapshot.Zone.Center);
            else
                PlaceMarker(hall!.Finished, hall.Centre);
        }
        catch (System.Exception ex)
        {
            _failed = true;
            IoC.Resolve<IModLogger>()?.LogError($"[StagedSiege] HUD failed, layer removed: {ex.Message}");
            TryDetach();
        }
    }

    public override void OnMissionScreenFinalize()
    {
        TryDetach();
        base.OnMissionScreenFinalize();
    }

    // Detach with no way to throw out of a screen callback; a failure here is only logged.
    private void TryDetach()
    {
        try
        {
            Detach();
        }
        catch (System.Exception ex)
        {
            IoC.Resolve<IModLogger>()?.LogError($"[StagedSiege] HUD detach failed: {ex.Message}");
            _vm = null;
            _layer = null;
            _movie = null;
        }
    }

    private void Attach()
    {
        _vm = new StagedSiegeHudVM();
        _layer = new GauntletLayer("GauntletLayer", 100, false);
        _movie = _layer.LoadMovie("StagedSiegeHud", _vm);
        MissionScreen.AddLayer(_layer);
        _sinceRefresh = RefreshIntervalSec;
    }

    private void Detach()
    {
        if (_layer != null)
        {
            if (_movie != null)
                _layer.ReleaseMovie(_movie);
            MissionScreen?.RemoveLayer(_layer);
        }

        _vm?.OnFinalize();
        _vm = null;
        _layer = null;
        _movie = null;
    }

    private void PlaceMarker(bool finished, Domain.StagePoint centre)
    {
        var camera = MissionScreen?.CombatCamera;
        if (_vm == null || camera == null || finished)
        {
            _vm?.PlaceMarker(false, 0f, 0f);
            return;
        }

        // Copied from RealmBordersMapView.Project: w < 0 is behind the camera; keep a margin off-screen. WorldToScreenInsideUsableArea
        // answers inside the usable area, so the extent below is the usable area too.
        float width = Screen.RealScreenResolutionWidth * ScreenManager.UsableArea.x;
        float height = Screen.RealScreenResolutionHeight * ScreenManager.UsableArea.y;
        float x = 0f, y = 0f, w = 0f;
        MBWindowManager.WorldToScreenInsideUsableArea(camera, new Vec3(centre.X, centre.Y, centre.Z + MarkerHeightMetres), ref x, ref y, ref w);
        bool visible = w > 0f && x > -0.2f * width && x < 1.2f * width && y > -0.2f * height && y < 1.2f * height;
        _vm.PlaceMarker(visible, x - width * 0.5f, y - height * 0.5f);
    }
}
