// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), map-view-release.
using System;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.MapViewRelease;

/// <summary>
/// Decides which cover of the campaign map releases its view, and reports (docs/features/map-view-release.md). Pure: every
/// engine call goes through <see cref="IMapViewReleaseAdapter"/>. The two layer edges run on the main thread for every scene
/// layer the game changes, so for any layer but the map's they end at the first checks: the switched-off latch, the toggle,
/// then whether a campaign runs at all, and only then the map screen, through the adapter.
///
/// <para>The release does not run at the cover. The engine raises the deactivation before <c>MapScreen.OnPause</c>, and the
/// release marks the view not ready, so the map screen would see it unready as the covering screen opens and raise the global
/// loading window over that screen. A due cover is only remembered (<c>_pending</c>); the release runs when the map's layer is
/// activated again. On a pop the engine activates a screen's layers first (<c>ScreenBase.HandleActivate</c>, then
/// <c>HandleResume</c>), so the release lands before the ready check that follows it: <c>MapScreen.OnActivate</c> runs
/// <c>HandleIfSceneIsReady</c>, and so does every later frame (<c>BeforeTick</c> or the idle tick). <c>MapScreen.OnResume</c>
/// has no ready check of its own.</para>
///
/// <para>It never throws: an error switches the release off for the rest of this game launch (the service is a process
/// singleton), which only means menus leave the render targets behind, as in the vanilla game.</para>
/// </summary>
public sealed class MapViewReleaseService : IMapViewReleaseService
{
    internal const int LogEvery = 20;

    private readonly IMapViewReleaseSettingsProvider _settings;
    private readonly IMapViewReleaseAdapter _adapter;
    private readonly IModLogger _logger;
    private readonly MapViewReleaseCounter _counter = new MapViewReleaseCounter();
    private object? _pending;
    private bool _off;

    public MapViewReleaseService(IMapViewReleaseSettingsProvider settings, IMapViewReleaseAdapter adapter, IModLogger logger)
    {
        _settings = settings;
        _adapter = adapter;
        _logger = logger;
    }

    public void OnSceneLayerDeactivated(object layer)
    {
        if (_off) return;

        try
        {
            if (!_settings.ReleaseEnabled) return;
            if (!_adapter.IsCampaignRunning) return;
            if (!_adapter.IsMapSceneLayer(layer)) return;
            if (_counter.Cover(_settings.ReleaseInterval)) _pending = layer;
        }
        catch (Exception ex)
        {
            SwitchOff(ex);
        }
    }

    public void OnSceneLayerActivated(object layer)
    {
        if (_pending == null || _off) return;

        try
        {
            if (!ReferenceEquals(layer, _pending))
            {
                // Another scene layer: the map's release stays due (a mission layer during a battle), unless the campaign it
                // belonged to has ended, or the layer is the current map's own: loading a save from a cover builds a new map
                // screen with a new scene layer, and the old mark can never match it.
                if (!_adapter.IsCampaignRunning || _adapter.IsMapSceneLayer(layer)) _pending = null;
                return;
            }

            _pending = null;
            if (!_settings.ReleaseEnabled) return;
            if (!_adapter.IsCampaignRunning) return;
            if (!_adapter.IsMapSceneLayer(layer)) return;

            _adapter.ReleaseSceneView(layer);
        }
        catch (Exception ex)
        {
            SwitchOff(ex);
            return;
        }

        _counter.Released();

        // The release has run: a failing log costs only the line. The lambda captures `this` only and is built inside the
        // `if`, so a return that logs nothing allocates nothing.
        var releases = _counter.Releases;
        if (releases == 1 || releases % LogEvery == 0)
            Safe(() => _logger.LogInfo(MapViewReleaseLines.BuildReleasedLine(_counter.Releases, _counter.Covers)));
    }

    public void LogInstall()
    {
        var toggleOn = false;
        var interval = MapViewReleaseCounter.DefaultInterval;
        try
        {
            toggleOn = _settings.ReleaseEnabled;
            interval = _settings.ReleaseInterval;
        }
        catch
        {
            // MCM that cannot be read at install: the line says what it could read.
        }

        Safe(() => _logger.LogInfo(MapViewReleaseLines.BuildInstallLine(toggleOn, interval)));
    }

    private void SwitchOff(Exception exception)
    {
        _off = true;
        _pending = null;
        Safe(() => _logger.LogError(MapViewReleaseLines.BuildSwitchedOffLine(exception)));
    }

    private static void Safe(Action step)
    {
        try { step(); }
        catch { /* a log line must never change what a screen change does */ }
    }
}
