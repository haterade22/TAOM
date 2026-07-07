using System;
using System.Diagnostics;
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
        _exitWindowActive = false;
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
        Emit(BattleLoadPhase.EncounterStart, $"mainPartySize={mainPartySize}");
    }

    public void LogMissionOpenNew(string missionName, string sceneName, string? encounterSummary)
    {
        if (!IsEnabled) return;
        var detail = $"mission='{missionName}' scene='{sceneName}'";
        if (!string.IsNullOrEmpty(encounterSummary)) detail += " " + encounterSummary;
        Emit(BattleLoadPhase.MissionOpenNew, detail);
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
        _exitWindowActive = false;
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.MissionInitialize, $"scene='{sceneName}'");
    }

    public void LogAgentEquipBegin(EquipmentSnapshot snapshot)
    {
        if (!IsEnabled) return;
        if (snapshot == null) return;
        try
        {
            Emit(BattleLoadPhase.AgentEquipBegin,
                $"agent#{snapshot.AgentIndex} '{snapshot.AgentName}' char='{snapshot.CharacterId}' culture='{snapshot.CultureId}' slots={snapshot.Slots?.Count ?? 0}");

            var lines = _formatter.Format(snapshot);
            if (lines != null)
            {
                foreach (var line in lines)
                    _logger.LogDebug($"{Tag}   {line}");
            }
        }
        catch (Exception ex) { SafeWarn("LogAgentEquipBegin", ex); }
    }

    public void LogAgentEquipOk(int agentIndex, string agentName)
    {
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.AgentEquipOk, $"agent#{agentIndex} '{agentName}'");
    }

    public void LogBattlePlayable(string sceneName, int agentCount)
    {
        if (!IsEnabled) return;
        Emit(BattleLoadPhase.BattlePlayable, $"scene='{sceneName}' agents={agentCount}");
    }

    // ---- Mission-exit lifecycle (issue #331) ----

    public bool IsExitWindowActive => _exitWindowActive;

    public void LogExitBegin(string missionName, string sceneName, int agentCount, int allAgentCount)
    {
        if (!IsEnabled) return;
        try
        {
            Interlocked.Exchange(ref _seq, 0);
            _stopwatch.Restart();
            _exitWindowActive = true;
            Emit(BattleLoadPhase.ExitBegin,
                $"mission='{missionName}' scene='{sceneName}' agents={agentCount}/{allAgentCount} {GcStats()}");
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
        Emit(BattleLoadPhase.MapResumed, $"isSaving={isSaving} {GcStats()}");
    }

    public void LogFirstMapTick(bool isSaving)
    {
        if (IsExitPhaseLoggable())
            Emit(BattleLoadPhase.FirstMapTick, $"isSaving={isSaving}");
        // Close unconditionally — the hook only calls this while the window is open, and a
        // mid-window toggle-off must not latch the window (only the LOGGING is gated).
        _exitWindowActive = false;
    }

    private bool IsExitPhaseLoggable() => IsEnabled && _exitWindowActive;

    // gen0/gen1/gen2 collection counts + managed heap size. Deltas between ExitBegin and
    // MapResumed expose a mission-end full GC (Common.MemoryCleanupGC) as the time sink.
    private static string GcStats()
    {
        try
        {
            long heapMb = GC.GetTotalMemory(forceFullCollection: false) / (1024 * 1024);
            return $"gc={GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)} heapMB={heapMb}";
        }
        catch { return "gc=<unavailable>"; }
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
