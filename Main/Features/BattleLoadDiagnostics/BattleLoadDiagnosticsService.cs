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
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.MissionInitialize, $"scene='{sceneName}' {MemStats()}");
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
