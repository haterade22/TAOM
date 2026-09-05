using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Features.BattleLoadDiagnostics;

public sealed class BattleLoadDiagnosticsService : IBattleLoadDiagnosticsService
{
    private const string Tag = "[BattleLoad]";

    private readonly IModLogger _logger;
    private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
    private readonly IEquipmentDumpFormatter _formatter;

    private readonly Stopwatch _stopwatch = new Stopwatch();

    // MissionState.TickLoading frames observed since Mission.Initialize returned, and the clock
    // reading at that return. Together they turn the previously-dark async wait into a number:
    // polls=1 with a large waitMs means the main thread was BLOCKED inside one frame (a native
    // spin, the #352 WaitForMeshesToBeLoaded shape); polls ~ waitMs/16 means healthy multi-frame
    // streaming. Those are opposite diagnoses. -1 means "origin not observed" -> waitMs omitted.
    // Interlocked because TickLoading and the phase hooks are not guaranteed the same thread.
    private int _loadingPolls;
    private long _waitStartMs = -1L;

    // Render-wait measurement (bundle b18f3441). _renderWaitStartMs is the clock reading at
    // FinishMissionLoadingDone, i.e. the moment Scene.ResumeLoadingRenderings kicked off shader
    // compilation; _lastRenderWaitEmitMs throttles the per-frame marker to 1 Hz. Both are -1 for
    // "not observed", which the formatter renders as an ABSENT token rather than a zero.
    private long _renderWaitStartMs = -1L;
    private long _lastRenderWaitEmitMs = -1L;

    internal const long RenderWaitEmitIntervalMs = 1000L;
    private int _seq;
    private volatile string _currentStatusLine = "phase=<none>";
    private volatile bool _exitWindowActive;
    private long _exitWindowOpenedUtcTicks; // 0 = closed; read via Interlocked (feeds ExitStallSampler)

    // A 429-agent arena audience drawn from 9 character kits produced 1,146 slot lines encoding
    // 11 distinct rows (2026-08-03 tournament repro). The dump is per-LOADOUT; only the phase
    // stamps are per-agent, so each distinct loadout is dumped once and every later agent wearing
    // it just cites `loadout=#N`. Locked because a torn Dictionary can spin the game thread
    // forever, and a diagnostic must never be the thing that hangs the game.
    private readonly object _loadoutLock = new object();
    private readonly Dictionary<string, int> _loadoutIds = new Dictionary<string, int>(StringComparer.Ordinal);
    private int _nextLoadoutId;

    internal const int MaxTrackedLoadouts = 512;

    public BattleLoadDiagnosticsService(
        IModLogger logger,
        IBattleLoadDiagnosticsSettingsProvider settings,
        IEquipmentDumpFormatter formatter)
    {
        _logger = logger;
        _settings = settings;
        _formatter = formatter;
    }

    public bool IsEnabled => _settings.IsEnabled;
    public string CurrentStatusLine => _currentStatusLine;

    public void ResetLifecycle()
    {
        // Window state transitions are UNCONDITIONAL — a stale exit window must close even
        // while the master toggle is off, or a mid-window toggle-off latches it and the next
        // map activation emits spurious Exit* lines (deep-review data-flow finding, 2026-07-06).
        CloseExitWindow();
        ResetLoadoutCache();
        ResetLoadingPollCounter();
        if (!IsEnabled) return;
        try
        {
            Interlocked.Exchange(ref _seq, 0);
            _stopwatch.Restart();
        }
        catch (Exception ex) { SafeWarn("ResetLifecycle", ex); }
    }

    public void LogEncounterStart(int mainPartySize)
    {
        if (!IsEnabled) return;
        // Encounter is the lifecycle origin — make sure the clock is running even if a
        // mission opened without PlayerEncounter.Start (e.g. arena/custom paths).
        try { if (!_stopwatch.IsRunning) _stopwatch.Restart(); } catch { /* clock best-effort */ }
        Emit(BattleLoadPhase.EncounterStart, $"mainPartySize={mainPartySize} {MemStats()}");
    }

    public void LogMissionOpenNew(string missionName, string sceneName, string? encounterSummary)
    {
        if (!IsEnabled) return;
        // The clock was previously started ONLY by LogEncounterStart/ResetLifecycle, both reachable
        // only from PlayerEncounter_Start_Patch — which is campaign-only. A CUSTOM BATTLE therefore
        // never started it, so every [BattleLoad] line in one read t=+0ms and all phase deltas were
        // lost. That is precisely the station the commit-attribution matrix uses for its battle
        // numbers, so the instrument was dead where it was most needed. MissionState.OpenNew is the
        // universal funnel (it fires for campaign, custom battle and arena alike), so the clock
        // starts here too. IsRunning-guarded, so the campaign path keeps EncounterStart as its
        // origin and existing deltas are unchanged.
        try { if (!_stopwatch.IsRunning) _stopwatch.Restart(); } catch { /* clock best-effort */ }
        var detail = $"mission='{missionName}' scene='{sceneName}'";
        if (!string.IsNullOrEmpty(encounterSummary)) detail += " " + encounterSummary;
        Emit(BattleLoadPhase.MissionOpenNew, detail);
    }

    public void LogMissionOpenNewDone(string missionName, bool missionCreated)
    {
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.MissionOpenNewDone, $"mission='{missionName}' created={missionCreated}");
    }

    public void LogLoadMissionBegin()
    {
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.LoadMissionBegin, string.Empty);
    }

    public void LogResourceClearOldBegin()
    {
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.ResourceClearOldBegin, string.Empty);
    }

    public void LogResourceClearOldDone()
    {
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.ResourceClearOldDone, string.Empty);
    }

    public void LogBattleSceneSelected(int mapIndex, string sceneId, bool isNaval)
    {
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.BattleSceneSelected, $"mapIndex={mapIndex} sceneId='{sceneId}' naval={isNaval}");
    }

    public void LogMissionInitialize(string sceneName)
    {
        // A mission starting means any still-open exit window is stale (chained mission
        // without map activation) — close it unconditionally before entry-phase logging.
        CloseExitWindow();
        ResetLoadoutCache();
        // Reset the loading-poll counter BEFORE the toggle gate: it is state, not I/O, so a
        // mid-load toggle-off must not leave a stale count for the next FinishMissionLoadingBegin
        // to report (latch rule 2, .claude/rules/harmony-patches.md).
        ResetLoadingPollCounter();
        if (!IsEnabled) return;
        // Belt-and-braces clock start for any path that reaches Initialize without OpenNew's
        // prefix having run (see the LogMissionOpenNew comment for why this matters).
        try { if (!_stopwatch.IsRunning) _stopwatch.Restart(); } catch { /* clock best-effort */ }
        Emit(BattleLoadPhase.MissionInitialize, $"scene='{sceneName}' {MemStats()}");
    }

    public void LogMissionInitializeDone(string sceneName)
    {
        // State first, gate second. Arming the wait clock and zeroing the poll counter are the
        // measurement's origin: if a toggle-off skipped them, the next FinishMissionLoadingBegin
        // would report a wait spanning the previous load.
        ResetLoadingPollCounter();
        try { Interlocked.Exchange(ref _waitStartMs, _stopwatch.ElapsedMilliseconds); }
        catch { Interlocked.Exchange(ref _waitStartMs, -1L); }
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.MissionInitializeDone, $"scene='{sceneName}' {MemStats()}");
    }

    // Counter ONLY — never logs. One call per MissionState.TickLoading frame; at 60fps a 12-second
    // wait is 720 frames, so a log line here would be worse than the blind spot it closes. Also
    // deliberately does NOT read IsEnabled: that would hit MCM's static Instance every frame, and
    // the count is state rather than I/O so it must survive a mid-load toggle.
    public void NoteLoadingPoll() => Interlocked.Increment(ref _loadingPolls);

    public void LogFinishMissionLoadingBegin()
    {
        var polls = Interlocked.CompareExchange(ref _loadingPolls, 0, 0);
        var startedAt = Interlocked.Read(ref _waitStartMs);
        if (!IsEnabled) return;

        long? waitMs = null;
        if (startedAt >= 0)
        {
            try
            {
                var now = _stopwatch.ElapsedMilliseconds;
                if (now >= startedAt) waitMs = now - startedAt;
            }
            catch { /* clock best-effort — omit rather than fabricate */ }
        }

        Emit(BattleLoadPhase.FinishMissionLoadingBegin, $"{FormatFinishWaitDetail(polls, waitMs)} {MemStats()}");
    }

    // Pure seam so the twin literal can be pinned character-for-character on both sides of the
    // C#/Python contract. waitMs is OMITTED, never rendered as 0, when the origin was not observed
    // — the same never-fabricate-a-zero rule MemStats() follows for its process tokens.
    internal static string FormatFinishWaitDetail(int polls, long? waitMs) =>
        waitMs.HasValue ? $"polls={polls} waitMs={waitMs.Value}" : $"polls={polls}";

    public void LogFinishMissionLoadingDone()
    {
        // State first, gate second — the same latch rule LogMissionInitializeDone follows for the
        // async-wait origin. FinishMissionLoading ends with Scene.ResumeLoadingRenderings(), so
        // this instant IS the start of the render wait; a toggle-off must not strand it.
        try { Interlocked.Exchange(ref _renderWaitStartMs, _stopwatch.ElapsedMilliseconds); }
        catch { Interlocked.Exchange(ref _renderWaitStartMs, -1L); }
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.FinishMissionLoadingDone, MemStats());
    }

    // Called once per MissionState.OnTick frame while the mission is loaded but not yet ticking.
    // Throttled to 1 Hz here rather than in the hook so the hook stays a guard plus one call, the
    // same division NoteLoadingPoll uses. Deliberately reads IsEnabled AFTER the throttle so a
    // disabled feature costs one subtraction per frame instead of an MCM static resolve.
    public void NoteWaitingForRender(int shadersInFlight)
    {
        long now;
        try { now = _stopwatch.ElapsedMilliseconds; }
        catch { return; /* clock best-effort — a diagnostic must never throw on the tick path */ }

        if (!ShouldEmitRenderWait(now, Interlocked.Read(ref _lastRenderWaitEmitMs), RenderWaitEmitIntervalMs)) return;
        Interlocked.Exchange(ref _lastRenderWaitEmitMs, now);
        if (!IsEnabled) return;

        long? waitedMs = null;
        long startedAt = Interlocked.Read(ref _renderWaitStartMs);
        if (startedAt >= 0 && now >= startedAt) waitedMs = now - startedAt;

        var detail = FormatRenderWaitDetail(waitedMs, shadersInFlight);
        Emit(BattleLoadPhase.WaitingForRender, detail.Length == 0 ? MemStats() : $"{detail} {MemStats()}");
    }

    // Pure seam. `lastEmitMs < 0` is the never-emitted case, so the first frame of a wait always
    // speaks. The `now < lastEmitMs` branch matters because the stopwatch RESTARTS between loads:
    // a stale stamp from a previous mission must not latch the marker off for this one.
    internal static bool ShouldEmitRenderWait(long nowMs, long lastEmitMs, long intervalMs) =>
        lastEmitMs < 0L || nowMs < lastEmitMs || nowMs - lastEmitMs >= intervalMs;

    // Pure seam so both tokens can be pinned character-for-character against triage_battle_load.py.
    // Either token is OMITTED when unmeasured, never rendered as 0 — a fabricated `shaders=0` in a
    // user log would read as "nothing was compiling", which is the opposite of "we could not read".
    internal static string FormatRenderWaitDetail(long? waitedMs, int shadersInFlight)
    {
        var wait = waitedMs.HasValue ? $"waitedMs={waitedMs.Value}" : string.Empty;
        var shaders = shadersInFlight >= 0 ? $"shaders={shadersInFlight}" : string.Empty;
        if (wait.Length == 0) return shaders;
        return shaders.Length == 0 ? wait : $"{wait} {shaders}";
    }

    // Per-load measurement state, cleared by every lifecycle origin (ResetLifecycle,
    // LogMissionInitialize, LogMissionInitializeDone). Unconditional by design: these are state,
    // not I/O, so a mid-load toggle must not leave the next load measuring from a stale origin.
    private void ResetLoadingPollCounter()
    {
        Interlocked.Exchange(ref _loadingPolls, 0);
        Interlocked.Exchange(ref _waitStartMs, -1L);
        Interlocked.Exchange(ref _renderWaitStartMs, -1L);
        Interlocked.Exchange(ref _lastRenderWaitEmitMs, -1L);
    }

    public void LogAgentEquipBegin(EquipmentSnapshot snapshot)
    {
        if (!IsEnabled) return;
        if (snapshot == null) return;
        try
        {
            // Formatted before the Begin line because the rendered rows ARE the loadout key.
            var lines = _formatter.Format(snapshot);
            bool isNewLoadout = TryClaimLoadout(snapshot, lines, out int loadoutId);

            Emit(BattleLoadPhase.AgentEquipBegin,
                $"agent#{snapshot.AgentIndex} '{snapshot.AgentName}' char='{snapshot.CharacterId}' culture='{snapshot.CultureId}'" +
                Opt(" race=", snapshot.Race) + Opt(" monster=", snapshot.MonsterId) + Opt(" actionSet=", snapshot.ActionSetName) +
                $" slots={snapshot.Slots?.Count ?? 0} loadout=#{loadoutId}" +
                OptRaw(" from=", snapshot.SpawnOrigin));

            if (!isNewLoadout || lines == null) return;
            foreach (var line in lines)
                _logger.LogDebug($"{Tag}   {line}");
        }
        catch (Exception ex) { SafeWarn("LogAgentEquipBegin", ex); }
    }

    // True when this loadout has not been dumped yet in the current load. The key is gear +
    // skeleton identity and deliberately NOT the character id: it must mean "what the engine is
    // about to assemble", which is the thing that can fault. So a mid-load MatchEquipment rewrite
    // (TaomTournamentModel.GetParticipantArmor) surfaces as a NEW id with a fresh dump rather than
    // being swallowed by the agent's earlier twin.
    private bool TryClaimLoadout(EquipmentSnapshot snapshot, IReadOnlyList<string>? lines, out int loadoutId)
    {
        var key = BuildLoadoutKey(snapshot, lines);
        lock (_loadoutLock)
        {
            if (_loadoutIds.TryGetValue(key, out loadoutId)) return false;

            loadoutId = ++_nextLoadoutId;
            // Past the cap the map stops growing and every agent dumps in full again. A load with
            // >512 distinct loadouts is already pathological, and the verbose log is the right
            // output for it — unbounded growth on the spawn path is the worse failure.
            if (_loadoutIds.Count < MaxTrackedLoadouts) _loadoutIds[key] = loadoutId;
            return true;
        }
    }

    // ASCII unit separator: no item id, mesh name or action-set name can contain it, so two
    // different loadouts cannot concatenate into the same key.
    private const char KeySep = '\u001f';

    private static string BuildLoadoutKey(EquipmentSnapshot snapshot, IReadOnlyList<string>? lines)
    {
        var sb = new StringBuilder()
            .Append(snapshot.Race).Append(KeySep)
            .Append(snapshot.MonsterId).Append(KeySep)
            .Append(snapshot.ActionSetName);
        if (lines != null)
        {
            foreach (var line in lines) sb.Append(KeySep).Append(line);
        }
        return sb.ToString();
    }

    // Unconditional, like CloseExitWindow: gating this on IsEnabled would let a mid-load
    // toggle-off strand a stale cache, and the next load would silently skip dumps it owes.
    private void ResetLoadoutCache()
    {
        lock (_loadoutLock)
        {
            _loadoutIds.Clear();
            _nextLoadoutId = 0;
        }
    }

    public void LogAgentEquipOk(int agentIndex, string agentName)
    {
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.AgentEquipOk, $"agent#{agentIndex} '{agentName}'");
    }

    // Must Emit (LogInfo), never LogDebug — DEBUG is the async path and a native crash drops it,
    // which is the exact failure this stamp exists to survive. Pinned by a test.
    public void LogAgentBuildDone(int agentIndex, string agentName)
    {
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.AgentBuildDone, $"agent#{agentIndex} '{agentName}'");
    }

    // Optional log tokens: an absent value drops its key entirely rather than emitting a bare
    // `race=` or a `<null>` that would read as a real engine value in a user-uploaded log.
    private static string Opt(string key, string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : $"{key}'{value}'";

    private static string OptRaw(string key, string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : $"{key}{value}";

    public void LogMissionAfterStartBegin()
    {
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.MissionAfterStartBegin, string.Empty);
    }

    public void LogMissionAfterStartDone()
    {
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.MissionAfterStartDone, string.Empty);
    }

    public void LogTaomBehaviorsBegin()
    {
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.TaomBehaviorsBegin, string.Empty);
    }

    // Must Emit (LogInfo), never LogDebug: DEBUG is the async path and a crash drops it, which is
    // the exact failure this stamp exists to survive. Pinned by a test.
    public void LogTaomBehaviorAdded(string behaviorName)
    {
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.TaomBehaviorAdded, $"behavior='{behaviorName}'");
    }

    public void LogTaomBehaviorsDone(int count)
    {
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.TaomBehaviorsDone, $"count={count}");
    }

    public void LogBattlePlayable(string sceneName, int agentCount)
    {
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.BattlePlayable, $"scene='{sceneName}' agents={agentCount} {MemStats()}");
    }

    // ---- Mission-exit lifecycle (issue #331) ----

    public bool IsExitWindowActive => _exitWindowActive;

    public long ExitWindowOpenedUtcTicks => Interlocked.Read(ref _exitWindowOpenedUtcTicks);

    public void LogExitBegin(string missionName, string sceneName, int agentCount, int allAgentCount)
    {
        if (!IsEnabled) return;
        try
        {
            Interlocked.Exchange(ref _seq, 0);
            _stopwatch.Restart();
            _exitWindowActive = true;
            Interlocked.Exchange(ref _exitWindowOpenedUtcTicks, DateTime.UtcNow.Ticks);
            Emit(BattleLoadPhase.ExitBegin,
                $"mission='{missionName}' scene='{sceneName}' agents={agentCount}/{allAgentCount} {MemStats()}");
        }
        catch (Exception ex) { SafeWarn("LogExitBegin", ex); }
    }

    public void LogExitTeardownBegin()
    {
        if (!IsExitPhaseLoggable()) return;
        Emit(BattleLoadPhase.ExitTeardownBegin, string.Empty);
    }

    public void LogExitTeardownDone()
    {
        if (!IsExitPhaseLoggable()) return;
        Emit(BattleLoadPhase.ExitTeardownDone, string.Empty);
    }

    public void LogExitStateFinalizeBegin()
    {
        if (!IsExitPhaseLoggable()) return;
        Emit(BattleLoadPhase.ExitStateFinalizeBegin, string.Empty);
    }

    public void LogExitStateFinalizeDone()
    {
        if (!IsExitPhaseLoggable()) return;
        Emit(BattleLoadPhase.ExitStateFinalizeDone, string.Empty);
    }

    public void LogExitResourceClearBegin(bool forceClearGpuResources)
    {
        if (!IsExitPhaseLoggable()) return;
        Emit(BattleLoadPhase.ExitResourceClearBegin, $"forceClearGpu={forceClearGpuResources}");
    }

    public void LogExitResourceClearDone()
    {
        if (!IsExitPhaseLoggable()) return;
        Emit(BattleLoadPhase.ExitResourceClearDone, string.Empty);
    }

    public void LogMapResumed(bool isSaving)
    {
        // #425 — MapResumed is the end of the TEARDOWN the stall sampler exists to watch.
        // Everything after it is player time: menus, conversations, loot screens (field-measured
        // 123 s in a quartermaster conversation with three [ERROR] samples fired into it).
        // Disarm the sampler here, unconditionally — same reasoning as CloseExitWindow's
        // toggle-off note — while _exitWindowActive stays true so FirstMapTick still logs the
        // time-to-playable tail for the phase record.
        Interlocked.Exchange(ref _exitWindowOpenedUtcTicks, 0L);

        if (!IsExitPhaseLoggable()) return;
        Emit(BattleLoadPhase.MapResumed, $"isSaving={isSaving} {MemStats()}");
    }

    public void LogFirstMapTick(bool isSaving)
    {
        if (IsExitPhaseLoggable())
            Emit(BattleLoadPhase.FirstMapTick, $"isSaving={isSaving}");
        // Close unconditionally — the hook only calls this while the window is open, and a
        // mid-window toggle-off must not latch the window (only the LOGGING is gated).
        CloseExitWindow();
    }

    private bool IsExitPhaseLoggable() => IsEnabled && _exitWindowActive;

    private void CloseExitWindow()
    {
        _exitWindowActive = false;
        Interlocked.Exchange(ref _exitWindowOpenedUtcTicks, 0L);
    }

    // gen0/gen1/gen2 collection counts + managed heap size, plus the process footprint
    // (privMB/wsMB via one GetProcessMemoryInfo syscall — #386). Deltas between ExitBegin and
    // MapResumed expose a mission-end full GC (Common.MemoryCleanupGC) as the time sink; the
    // process tokens anchor the phase line against the periodic [MemSample] trajectory. On
    // reader failure both process tokens are omitted (never a fabricated 0 in a user log).
    private static string MemStats()
    {
        string gcStats;
        try
        {
            long heapMb = GC.GetTotalMemory(forceFullCollection: false) / (1024 * 1024);
            gcStats = $"gc={GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)} heapMB={heapMb}";
        }
        catch { gcStats = "gc=<unavailable>"; }

        return MemorySampleReader.TryReadProcess(out long privMb, out long wsMb)
            ? $"{gcStats} privMB={privMb} wsMB={wsMb}"
            : gcStats;
    }

    // Single choke point: increment the sequence, stamp elapsed ms, update the status line
    // for the watchdog, and write the marker. The status line is updated BEFORE the
    // (potentially throwing) log write so the watchdog sees the latest phase even if the
    // sink hiccups.
    private void Emit(BattleLoadPhase phase, string detail)
    {
        try
        {
            int seq = Interlocked.Increment(ref _seq);
            long ms = 0;
            try { ms = _stopwatch.ElapsedMilliseconds; } catch { /* clock best-effort */ }

            _currentStatusLine = $"phase={phase} seq={seq} {detail}";
            _logger.LogInfo($"{Tag} seq={seq} t=+{ms}ms phase={phase} {detail}");
        }
        catch (Exception ex) { SafeWarn("Emit", ex); }
    }

    private void SafeWarn(string where, Exception ex)
    {
        try { _logger.LogWarning($"{Tag} {where} failed: {ex.GetType().Name}: {ex.Message}"); }
        catch { /* the diagnostic must never propagate */ }
    }
}
