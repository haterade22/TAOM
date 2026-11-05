// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), nameplate-cull.
using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.NameplateCull.Models;

namespace TAOM.Features.NameplateCull;

/// <summary>
/// Decides whether the nameplate update runs culled, and reports (docs/features/nameplate-cull.md). Pure: every engine call
/// goes through <see cref="INameplateCullAdapter"/>. <see cref="TryUpdate"/> runs once per campaign-map frame on the main
/// thread, so its state needs no lock, and it never throws: any failure hands the frame to vanilla. The service is a
/// process singleton, so "off" lasts until the game is launched again.
///
/// <para>Falling back to vanilla after an error re-runs the whole update for that frame, at most once per launch. The replay
/// is best effort, not exact: values are recomputed from the camera and the settlements, but a plate that had already
/// pushed its bindings ticks its notifications a second time (one tick of notification merging), and an engine fault need
/// not repeat in vanilla's run if a setter stored its value before its change callback threw.</para>
/// </summary>
public sealed class NameplateCullService : INameplateCullService
{
    /// <summary>
    /// Culled map frames per log line (about five minutes at 60 fps). Counted in frames, not time: a game menu, a map
    /// conversation or the paused map still run the update every frame, and a toggle-off frame, a battle or a covering screen
    /// never reaches <see cref="Record"/>.
    /// </summary>
    internal const int WindowFrames = 18000;

    private readonly INameplateCullSettingsProvider _settings;
    private readonly INameplateCullAdapter _adapter;
    private readonly IModLogger _logger;

    private bool _available;
    private bool _off;
    private long _frames;
    private long _updated;
    private long _skipped;

    public NameplateCullService(INameplateCullSettingsProvider settings, INameplateCullAdapter adapter, IModLogger logger)
    {
        _settings = settings;
        _adapter = adapter;
        _logger = logger;
    }

    public bool TryUpdate(object nameplates)
    {
        if (!_available || _off) return false;
        if (!ToggleOn()) return false;

        CullCounts counts;
        try
        {
            counts = _adapter.UpdateCulled(nameplates);
        }
        catch (Exception ex)
        {
            SwitchOff(ex);
            return false;
        }

        // The update has run: a failure from here on costs a log line, never the frame.
        Record(counts);
        return true;
    }

    public void Install()
    {
        string? reason;
        try
        {
            reason = _adapter.Initialize();
        }
        catch (Exception ex)
        {
            reason = ex.GetType().Name + ": " + ex.Message;
        }

        if (reason == null)
        {
            _available = true;
            Safe(() => _logger.LogInfo(NameplateCullLines.BuildInstallLine(ToggleOn())));
        }
        else
        {
            _available = false;
            Safe(() => _logger.LogWarning(NameplateCullLines.BuildUnavailableLine(reason)));
        }
    }

    private bool ToggleOn()
    {
        try { return _settings.CullEnabled; }
        catch { return false; }
    }

    private void Record(CullCounts counts)
    {
        _frames++;
        _updated += counts.Updated;
        _skipped += counts.Skipped;

        if (_frames < WindowFrames) return;

        var frames = _frames;
        var updated = _updated;
        var skipped = _skipped;
        _frames = 0;
        _updated = 0;
        _skipped = 0;

        // No lambda here: it would capture locals and allocate a closure on every frame, not only at the end of a window.
        try { _logger.LogInfo(NameplateCullLines.BuildWindowLine(frames, updated, skipped)); }
        catch { /* the frame is already done: a failing log costs only the line */ }
    }

    private void SwitchOff(Exception exception)
    {
        _off = true;
        Safe(() => _logger.LogError(NameplateCullLines.BuildSwitchedOffLine(exception)));
    }

    private static void Safe(Action step)
    {
        try { step(); }
        catch { /* a log line must never change what the frame does */ }
    }
}
