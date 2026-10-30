using System.Globalization;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.LoadTimeStamps;

namespace TAOM.Features.ShadowfaxScreens.Clan;

/// <summary>
/// Clan fast open (docs/features/shadowfax-screens.md "Clan fast open"). Vanilla builds the clan screen's party lists
/// three times per open; the second and third builds are the ones <c>ClanPartiesVM.RefreshValues</c> triggers for the
/// same instance a moment after the first. A transpiler swaps the one <c>RefreshPartiesList()</c> call inside
/// <c>RefreshValues</c> for <see cref="Hooks.ClanPartiesVM_RefreshValues_ClanFastOpen_Patch.RefreshPartiesListInRefresh"/>, which asks this service whether to
/// skip; any other caller of the list builder (the screen's category refresh, a co-op handler) is untouched and always
/// builds. No TaleWorlds types. Every call runs on the UI main thread.
/// </summary>
internal sealed class ClanScreenFastPathService
{
    private readonly IShadowfaxScreensSettingsProvider _settings;
    private readonly ICoopPresenceProvider _coop;
    private readonly IStampClock _clock;
    private readonly IModLogger _logger;

    private bool _windowOpen;
    private bool _windowFastOn;
    private int _run;
    private int _skipped;
    private long _runTicks;

    public ClanScreenFastPathService(IShadowfaxScreensSettingsProvider settings, ICoopPresenceProvider coop, IStampClock clock, IModLogger logger)
    {
        _settings = settings;
        _coop = coop;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>True when the build at the swapped call site should be skipped (fast path on); counts the skip inside an open window.</summary>
    public bool ShouldSkipListBuild()
    {
        if (!FastPathOn()) return false;
        if (_windowOpen) _skipped++;
        return true;
    }

    /// <summary>Writes the one install line: the swapped-site count the transpiler stored, or a warning that the screen stays vanilla.</summary>
    public void LogInstall(int swappedSites)
    {
        try
        {
            if (swappedSites == 1) _logger.LogInfo("[Shadowfax] Clan fast open installed: 1 list build site swapped in ClanPartiesVM.RefreshValues");
            else _logger.LogWarning("[Shadowfax] Clan fast open not installed: expected exactly 1 RefreshPartiesList call in ClanPartiesVM.RefreshValues, swapped " +
                swappedSites + "; the Clan screen builds as vanilla does");
        }
        catch { /* a log line must never break startup */ }
    }

    /// <summary>Starts counting list builds for one screen open.</summary>
    public void BeginOpen()
    {
        _run = 0;
        _skipped = 0;
        _runTicks = 0;
        _windowFastOn = FastPathOn();
        _windowOpen = true;
    }

    /// <summary>Closes the counting window and returns the detail text for the open line (empty when no window was open).</summary>
    public string EndOpen()
    {
        if (!_windowOpen) return "";
        _windowOpen = false;
        var ms = LoadTimeStampLines.ToMs(_runTicks, _clock.Frequency);
        return string.Format(CultureInfo.InvariantCulture, "fast open {0}; list builds: {1} run, {2} skipped, {3:0.0} ms",
            _windowFastOn ? "on" : "off", _run, _skipped, ms);
    }

    /// <summary>The start stamp of a build that runs, or 0 when no window is open (nothing is then counted).</summary>
    public long BeginBuild() => _windowOpen ? _clock.Now : 0;

    public void EndBuild(long startTick)
    {
        if (startTick == 0 || !_windowOpen) return;
        _run++;
        _runTicks += _clock.Now - startTick;
    }

    private bool FastPathOn()
    {
        try { return _settings.ClanFastOpen && !_coop.IsCoopActive; }
        catch { return false; }
    }
}
