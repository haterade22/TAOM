using System;
using System.Text;
using TaleWorlds.ScreenSystem;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// Screen-transition memory anchors (#386 follow-up): one <c>[MemStation]</c> line per screen
/// open and close, so a session log says WHICH screen the commit growth happened on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why.</b> A measured session went 10,646 to 19,032 MB private bytes across 37 minutes with
/// ZERO missions. The battle lifecycle is already anchored on eight phase lines and showed stable
/// per-mission baselines (each battle cost ~2-3 GB and returned it), so the growth is on the
/// map/UI path, which had no anchors at all. The periodic 30s <c>[MemSample]</c> trace can say
/// that memory rose; only an anchor can say what the player opened when it did.
/// </para>
/// <para>
/// <b>No Harmony patch.</b> <c>ScreenManager.OnPushScreen</c> / <c>OnPopScreen</c> are public
/// static events (verified against installed v1.4.8). <c>OnPushScreen</c> is raised from four
/// methods and <c>OnPopScreen</c> from four (<c>PushScreen</c>, <c>PopScreen</c>,
/// <c>ReplaceTopScreen</c>, <c>SetAndActivateRootScreen</c>, <c>CleanAndPushScreen</c>,
/// <c>CleanScreens</c>, and the <c>DeactivateAndFinalizeAllScreens</c> teardown loop). One
/// subscription covers <c>MapScreen</c>, <c>MissionScreen</c>, <c>GauntletInventoryScreen</c>,
/// <c>GauntletPartyScreen</c>, <c>GauntletClanScreen</c>, <c>GauntletKingdomScreen</c> and
/// <c>CharacterCreationScreen</c> at no per-frame cost.
/// </para>
/// <para>
/// <b>KNOWN GAP: the encyclopedia is invisible to this instrument.</b> It is NOT a
/// <c>ScreenBase</c> and there is no <c>EncyclopediaState</c>; it is a <c>MapEncyclopediaView</c>
/// (a <c>MapView</c> overlay) added onto <c>MapScreen</c> and tracked by an
/// <c>IsEncyclopediaOpen</c> bool, so it is never pushed through <c>ScreenManager</c> and NO
/// station line is ever emitted when the player opens or closes it. <c>TopScreen</c> stays
/// <c>MapScreen</c> throughout. This matters because encyclopedia browsing is the leading suspect
/// for the growth this class was built to localise: covering it needs its own anchor off
/// <c>MapEncyclopediaView</c>, which is not built. Until then, use
/// <c>taom.print_memory &lt;label&gt;</c> by hand either side of an encyclopedia session.
/// </para>
/// <para>
/// <c>MapState.OnTick</c> was evaluated as an alternative and rejected: <c>GameStateManager.OnTick</c>
/// ticks only the ACTIVE state, so it does not run while an inventory, party, clan or kingdom
/// screen is on top. (That reasoning does NOT extend to the encyclopedia, where <c>MapState</c>
/// remains active — but neither approach observes it, per the gap above.)
/// </para>
/// <para>
/// <b>The risk this class is shaped around.</b> Those events are plain multicast delegates and
/// not one of the raise sites is wrapped in a try/catch, so an exception from our handler skips
/// every later subscriber and unwinds straight out of <c>ScreenManager.PushScreen</c> — breaking
/// screen navigation for every mod in the process. Hence try/catch at the outermost statement of
/// both handlers AND swallow-and-warn inside <see cref="NoteStation"/> (the tested path, so the
/// guarantee is pinned rather than assumed). Same construction as MemoryPressureSampler.PollOnce.
/// </para>
/// <para>
/// <b>Threading: convention, not enforcement.</b> The unsynchronised fields below are safe only
/// because every engine caller of these APIs is UI-thread code. The engine's own guard is
/// <c>Debug.FailedAssert("Screen should be changed from main thread")</c>, which does NOT throw
/// and whose result nothing checks, and it is absent entirely from <c>ReplaceTopScreen</c> and
/// <c>SetAndActivateRootScreen</c>. So this is caller discipline rather than a guarantee. It is
/// accepted here because the cost of being wrong is a torn counter in a diagnostic, not corrupt
/// game state; if that ever stops being true, add the <c>Interlocked</c> guard the sibling
/// samplers use.
/// </para>
/// <para>
/// The line literals are a cross-lane contract: <c>tools/triage_battle_load.py</c> parses them
/// and twin literal tests pin both sides. Change them here first, then the Python twin.
/// </para>
/// </remarks>
public sealed class MemoryStationSampler : IDisposable
{
    private const string Tag = "[MemStation]";
    internal const string UnknownScreenName = "<unknown>";

    /// <summary>
    /// Session cap, deliberately a CAP and not a rate limit: rate-limiting would drop an
    /// <c>exit</c> and orphan its <c>enter</c>, which destroys the delta the line exists to
    /// produce. Past the cap the sampler says so once and goes quiet.
    /// </summary>
    internal const int MaxLinesPerSession = 2000;

    /// <summary>A CLR type name cannot contain a quote, but generics carry ` and [.</summary>
    internal const int MaxScreenNameLength = 64;

    private readonly IModLogger _logger;
    private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
    private readonly int _cap;

    private int _emitted;
    private bool _capReported;
    private bool _started;

    public MemoryStationSampler(IModLogger logger, IBattleLoadDiagnosticsSettingsProvider settings)
        : this(logger, settings, MaxLinesPerSession)
    {
    }

    // Test seam: inject the cap so the past-the-cap behaviour is unit-tested without 2,000 calls.
    // internal (not public) so DryIoc sees a single public ctor and auto-resolves it;
    // TAOM.Tests reaches this via InternalsVisibleTo (see TAOM.csproj).
    internal MemoryStationSampler(IModLogger logger, IBattleLoadDiagnosticsSettingsProvider settings, int cap)
    {
        _logger = logger;
        _settings = settings;
        _cap = cap;
    }

    /// <summary>How many times <see cref="Start"/> actually subscribed. Pins idempotency.</summary>
    internal int SubscribeCount { get; private set; }

    /// <summary>
    /// Idempotent. Load-bearing: the hook this is started from
    /// (<c>OnBeforeInitialModuleScreenSetAsRoot</c>) re-fires on EVERY return to the main menu,
    /// so a second subscription would double every line for the rest of the process.
    /// </summary>
    public void Start()
    {
        // Reset FIRST, and outside the subscription latch. This hook re-fires on every return to
        // the main menu, which is the only session boundary this class sees; without this the
        // "per session" cap below is really a per-PROCESS cap, and a second campaign in the same
        // process would inherit an exhausted budget and log nothing at all, with its one
        // cap-reached warning sitting in a previous campaign's log. That is the process-scoped
        // diagnostic-latch defect this repo has shipped before.
        // A reset mid-log is a discontinuity in the very artefact being analysed, so mark it.
        // Without this the parser sees two 2,000-line segments as one series, cannot clear
        // enters left pending across the boundary, and cannot tell a fresh budget from a
        // continuous one. Emitted BEFORE the reset and only on a re-entry, uncapped so the
        // marker itself can never be the line the cap swallows.
        if (_started)
        {
            try { _logger.LogInfo(FormatSessionReset(_cap)); } catch { }
        }
        ResetSessionBudget();

        if (_started) return;

        // Subscribe BEFORE latching. If the second += ever threw with the latch already set, the
        // sampler would be permanently half-subscribed (enters, no exits) and every later Start()
        // a silent no-op — and since deltas come from PAIRS, that yields a log that looks healthy
        // and measures nothing.
        ScreenManager.OnPushScreen += HandlePush;
        try
        {
            ScreenManager.OnPopScreen += HandlePop;
        }
        catch
        {
            // Roll the first one back rather than leaving a push-only subscription behind: the
            // next Start() would then add a SECOND push handler and double every enter line
            // while still emitting no exits, so deltas would be unobtainable and the counts wrong.
            ScreenManager.OnPushScreen -= HandlePush;
            throw;
        }
        _started = true;
        SubscribeCount++;
    }

    /// <summary>Clears the per-session emit budget. Internal so tests can drive it directly.</summary>
    internal void ResetSessionBudget()
    {
        _emitted = 0;
        _capReported = false;
    }

    // Outermost-statement guard: nothing may ever escape into the engine's delegate chain.
    private void HandlePush(ScreenBase pushedScreen)
    {
        try { NoteStation("enter", pushedScreen?.GetType().Name); } catch { }
    }

    private void HandlePop(ScreenBase poppedScreen)
    {
        try { NoteStation("exit", poppedScreen?.GetType().Name); } catch { }
    }

    /// <summary>
    /// Reads memory and writes one station line. Internal seam so tests drive it without the
    /// engine; swallow-and-warn lives HERE (not only in the handlers) so the tested path proves a
    /// throwing logger cannot propagate.
    /// </summary>
    internal void NoteStation(string kind, string? rawScreenName)
    {
        try
        {
            // Gate ONLY on the sampler's own toggle, matching MemoryPressureSampler: the master
            // toggle governs battle-load PHASE logging, and turning that off must not silently
            // kill session-wide memory forensics.
            if (!_settings.MemorySamplerEnabled) return;

            if (!ShouldEmit(_emitted, _cap))
            {
                if (_capReported) return;
                _capReported = true;
                _logger.LogWarning(FormatCapReached(_cap));
                return;
            }

            // Omit on read failure — never a fabricated zero in a user log.
            if (!MemorySampleReader.TryRead(out var sample)) return;

            _emitted++;
            // LogInfo, not LogDebug: INFO flushes synchronously and survives a hard crash, which
            // is the failure this anchor exists to explain. DEBUG rides an async writer.
            _logger.LogInfo(FormatStation(kind, SanitizeScreenName(rawScreenName), sample));
        }
        catch (Exception ex)
        {
            try { _logger.LogWarning($"{Tag} station failed: {ex.GetType().Name}: {ex.Message}"); }
            catch { /* never propagate into ScreenManager's delegate chain */ }
        }
    }

    // ---- Pure seams -----------------------------------------------------------------------

    internal static bool ShouldEmit(int emitted, int cap) => emitted < cap;

    /// <summary>
    /// Log-forgery guard. The name is echoed into a file a Python tool parses, so a quote could
    /// close the <c>screen='...'</c> token and a newline could forge an entire line. Same
    /// reasoning as MemoryProbeReportFormatter's station-label validator.
    /// </summary>
    internal static string SanitizeScreenName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return UnknownScreenName;

        var sb = new StringBuilder(Math.Min(raw!.Length, MaxScreenNameLength));
        foreach (var ch in raw)
        {
            if (sb.Length >= MaxScreenNameLength) break;
            bool safe = (ch >= 'A' && ch <= 'Z')
                        || (ch >= 'a' && ch <= 'z')
                        || (ch >= '0' && ch <= '9')
                        || ch == '_' || ch == '.' || ch == '-';
            sb.Append(safe ? ch : '_');
        }
        return sb.ToString();
    }

    internal static string FormatStation(string kind, string screen, in MemorySample s)
        => $"{Tag} {kind} screen='{screen}' {MemoryPressureSampler.FormatSampleTokens(in s)}";

    /// <summary>
    /// Wording matters: silence after the cap must never read as a clean result. Same shape as
    /// TableauDiagnostics' "census is FULL" line.
    /// </summary>
    /// <summary>
    /// Session boundary marker. Deliberately NOT matched by the station regex: the Python side
    /// recognises it by its own literal and uses it to segment, so it can never be mistaken for
    /// a measurement.
    /// </summary>
    internal static string FormatSessionReset(int cap)
        => $"{Tag} session-reset reason=main-menu budget={cap}";

    internal static string FormatCapReached(int cap)
        => $"{Tag} cap reached after {cap} lines, later screen transitions are NOT measured. "
           + "A missing station below this point means 'not measured', not 'no growth'.";

    public void Dispose()
    {
        if (!_started) return;
        // Unsubscribe before clearing the latch, mirroring Start()'s order.
        ScreenManager.OnPushScreen -= HandlePush;
        ScreenManager.OnPopScreen -= HandlePop;
        _started = false;
    }
}
