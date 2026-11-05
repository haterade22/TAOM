using System.Collections.Generic;
using System.Globalization;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.LoadTimeStamps;

namespace TAOM.Features.ShadowfaxScreens.Inventory;

/// <summary>
/// The decisions and counters behind the Inventory fast open (docs/features/shadowfax-screens.md): the toggle
/// gate, one install or bail line for the name-sort rewrite, and the per-open totals that go on the screen's
/// [ScreenOpen] line. Holds no TaleWorlds type (ADR-007). UI main thread only. The settings provider never throws
/// (IShadowfaxScreensSettingsProvider), so none of this wraps a read in a catch.
/// </summary>
internal sealed class InventoryOpenFastPathService
{
    private readonly IShadowfaxScreensSettingsProvider _settings;
    private readonly IStampClock _clock;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _reportedSites = new();

    private bool _openActive;
    private long _rowTicks;
    private int _sortsSkipped;
    private bool _fastOpen;

    public InventoryOpenFastPathService(IShadowfaxScreensSettingsProvider settings, IStampClock clock, IModLogger logger)
    {
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Read per call, so the MCM switch applies to the next open.</summary>
    public bool Enabled => _settings.InventoryFastOpen;

    public bool OpenActive => _openActive;

    /// <summary>
    /// Logs one line per site name, however often it is reported (a re-applied category installs again). A bail line
    /// carries the rewrite's reason.
    /// </summary>
    public void ReportSite(string site, int swapped, int expected, string? problem)
    {
        if (!_reportedSites.Add(site)) return;
        var counts = string.Format(CultureInfo.InvariantCulture, "swapped {0} of {1} call sites", swapped, expected);
        if (swapped == expected) _logger.LogInfo($"[ShadowfaxInventory] {site}: {counts}");
        else _logger.LogWarning($"[ShadowfaxInventory] {site}: left vanilla ({counts}): {problem ?? "no reason given"}");
    }

    /// <summary>Whether InitializeInventory's name sort may pass its source through (the ctor re-sorts the lists).</summary>
    public bool ShouldSkipNameSort()
    {
        if (!Enabled) return false;
        if (_openActive) _sortsSkipped++;
        return true;
    }

    /// <summary>Starts the totals of one screen open. Called only when the open is being timed.</summary>
    public void BeginOpen()
    {
        _openActive = true;
        _rowTicks = 0;
        _sortsSkipped = 0;
        _fastOpen = Enabled;
    }

    /// <summary>Starts the InitializeInventory phase, or returns null outside a timed open (a Reset re-runs it).</summary>
    public long? BeginRows()
    {
        if (!_openActive) return null;
        return _clock.Now;
    }

    public void EndRows(long? start)
    {
        if (start == null || !_openActive) return;
        _rowTicks += _clock.Now - start.Value;
    }

    /// <summary>
    /// Closes the open (the latch first, so no later row is counted) and returns the detail for its [ScreenOpen]
    /// line, led by the fast path the open ran with. A negative count prints n/a.
    /// </summary>
    public string EndOpen(string? mode, int left, int right)
    {
        _openActive = false;
        var ms = LoadTimeStampLines.ToMs(_rowTicks, _clock.Frequency);
        return string.Format(CultureInfo.InvariantCulture,
            "fast open {0}; mode={1}, left={2}, right={3}, rows={4:0.0} ms, sorts skipped={5}",
            _fastOpen ? "on" : "off", mode ?? "n/a", Count(left), Count(right), ms, _sortsSkipped);
    }

    private static string Count(int value) => value < 0 ? "n/a" : value.ToString(CultureInfo.InvariantCulture);
}
