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
