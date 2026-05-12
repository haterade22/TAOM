using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild.Checkpoint;
using TAOM.Features.EditorCacheRebuild.Diff;
using TAOM.Features.EditorCacheRebuild.Phase1;
using TAOM.Features.EditorCacheRebuild.Phase2;
using TAOM.Features.EditorCacheRebuild.Progress;
using TAOM.Features.EditorCacheRebuild.Validation;

namespace TAOM.Features.EditorCacheRebuild;

public class CacheBuilderService : IDistanceCacheBuilderService
{
    private readonly SerialPhase1Builder _serialPhase1;
    private readonly ParallelPhase1Builder _parallelPhase1;
    private readonly SerialPhase2Builder _serialPhase2;
    private readonly ParallelPhase2Builder _parallelPhase2;
    private readonly ISmokeTestGate _smokeTestGate;
    private readonly IValidationReportWriter _reportWriter;
    private readonly ICheckpointSerializer _checkpointSerializer;
    private readonly ISettlementSnapshotStore _snapshotStore;
    private readonly ISettlementDiffer _differ;
    private readonly ICacheRebuildConfigProvider _configProvider;
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    public CacheBuilderService(
        SerialPhase1Builder serialPhase1,
        ParallelPhase1Builder parallelPhase1,
        SerialPhase2Builder serialPhase2,
        ParallelPhase2Builder parallelPhase2,
        ISmokeTestGate smokeTestGate,
        IValidationReportWriter reportWriter,
        ICheckpointSerializer checkpointSerializer,
        ISettlementSnapshotStore snapshotStore,
        ISettlementDiffer differ,
        ICacheRebuildConfigProvider configProvider,
        IPathService pathService,
        IModLogger logger)
    {
        _serialPhase1 = serialPhase1;
        _parallelPhase1 = parallelPhase1;
        _serialPhase2 = serialPhase2;
        _parallelPhase2 = parallelPhase2;
        _smokeTestGate = smokeTestGate;
        _reportWriter = reportWriter;
        _checkpointSerializer = checkpointSerializer;
        _snapshotStore = snapshotStore;
        _differ = differ;
        _configProvider = configProvider;
        _pathService = pathService;
        _logger = logger;
    }

    public CacheBuildResult Build(INavigationCacheAdapter adapter, CancellationToken ct)
    {
        var config = _configProvider.GetConfig();
        var effectiveParallelism = config.Parallelism;
        SmokeTestResult smokeResult = default;
        var mode = "full";
        IPhase1Filter? phase1Filter = null;
        var overallSw = System.Diagnostics.Stopwatch.StartNew();

        BannerLogger.LogBanner(_logger, "CACHE REBUILD START");
        var navTypeName = adapter.NavigationType.ToString();
        var settlementCount = adapter.GetAllRegisteredSettlements().Count;
        _logger.LogInfo($"[CacheRebuild] NavigationType={navTypeName}, settlements={settlementCount}, parallelism={effectiveParallelism}");
        LogConfigSummary(config);

        var checkpointDir = ResolveCheckpointDir(config);
        var snapshotPath = ResolveSnapshotPath(config);
        var resumed = false;
        var willDeserialize = false;
        HashSet<string>? incrementalChangedIds = null;

        // Resume detection — checkpoint takes priority over incremental.
        if (config.EnableCheckpoint && !string.IsNullOrWhiteSpace(checkpointDir))
        {
            var loaded = _checkpointSerializer.TryLoad(checkpointDir, navTypeName, adapter);
            if (loaded != null && loaded.PhaseCompleted >= 1)
            {
                _logger.LogInfo($"[CacheRebuild] RESUMING from checkpoint (phase {loaded.PhaseCompleted} previously completed, written {loaded.Timestamp:u})");
                resumed = true;
                mode = "resumed";
                willDeserialize = true;
            }
        }

        // Incremental detection — only when not resuming.
        if (!resumed && config.EnableIncremental && !string.IsNullOrWhiteSpace(snapshotPath))
        {
            var diff = _differ.Compute(_snapshotStore.TryLoad(snapshotPath), adapter, config.IncrementalMaxChanged);
            if (!diff.ForcedFullRebuild && diff.TotalChanged > 0)
            {
                LogDiffDetails(diff);
                try
                {
                    adapter.DeserializeCache(GetFinalCachePath(checkpointDir, navTypeName));
                    incrementalChangedIds = diff.AllChangedIds();
                    phase1Filter = new ChangedSettlementsFilter(incrementalChangedIds);
                    mode = "incremental";
                    willDeserialize = true;
                    _logger.LogInfo($"[CacheRebuild] INCREMENTAL mode: Phase 1 will run on {incrementalChangedIds.Count} affected settlements only");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"[CacheRebuild] failed to load prior cache for incremental ({ex.GetType().Name}: {ex.Message}); falling back to FULL");
                    phase1Filter = null;
                }
            }
            else if (diff.ForcedFullRebuild)
            {
                _logger.LogInfo($"[CacheRebuild] FULL rebuild: {diff.Reason}");
            }
            else
            {
                _logger.LogInfo("[CacheRebuild] no settlement changes detected; doing full rebuild (no-op skip not implemented in v1)");
            }
        }

        // Phase 0 only runs for fresh full rebuilds. Codex Finding 2 (P1): when we deserialize old
        // cache state, that state already includes a valid _closestSettlementsToFaceIndices (CRC
        // verified matching), so running Phase 0 first would be wasted work that deserialize then
        // replaces. Running Phase 0 AFTER deserialize would throw on duplicate face ids (Add-only).
        if (!willDeserialize)
        {
            _logger.LogInfo("[CacheRebuild] Phase 0: GenerateClosestSettlementToFaceCache (vanilla helper)");
            adapter.RunClosestSettlementCache();
        }
        else
        {
            _logger.LogInfo("[CacheRebuild] Phase 0 SKIPPED — closest-face cache provided by deserialized state");
        }

        // Codex Finding 1 (P1): incremental mode loaded the FULL old distance dict. Phase 1
        // RunFiltered would call vanilla SetSettlementToSettlementDistanceWithLandRatio which
        // ends in `Dictionary.Add` → ArgumentException on existing keys. Pre-clean affected entries.
        if (mode == "incremental" && incrementalChangedIds != null && incrementalChangedIds.Count > 0)
        {
            _logger.LogInfo($"[CacheRebuild] Removing stale distance entries for {incrementalChangedIds.Count} changed settlements before Phase 1");
            adapter.RemoveDistanceEntriesFor(incrementalChangedIds);
        }

        // Codex Finding 2 (P1): vanilla GenerateNeighborSettlementsCache opens with
        // `_fortificationNeighbors.Clear()`. Our Phase 2 builders don't. When we deserialized
        // (resume OR incremental), the dict is non-empty; appending without clearing leaves stale
        // neighbor edges in the final output.
        // In resume mode the checkpoint was saved BEFORE Phase 2 → neighbors should be empty,
        // but the clear is cheap and defensive.
        if (willDeserialize)
        {
            adapter.ClearFortificationNeighbors();
        }

        if (!resumed && effectiveParallelism > 1)
        {
            _logger.LogInfo($"[CacheRebuild] Running smoke test gate ({config.SmokeTestPairs} pairs, tolerance={config.SmokeTestDistanceTolerance:E2})");
            smokeResult = _smokeTestGate.Run(adapter, ct);
            if (!smokeResult.IsSafeForParallel)
            {
                _logger.LogWarning($"[CacheRebuild] SMOKE TEST FAILED: {smokeResult.Reason ?? smokeResult.Outcome.ToString()}. Falling back to parallelism=1");
                effectiveParallelism = 1;
            }
        }

        var phase1 = SelectPhase1(effectiveParallelism);
        var phase2 = SelectPhase2(effectiveParallelism);
        _logger.LogInfo($"[CacheRebuild] EXECUTION PLAN: mode={mode}, phase1={phase1.GetType().Name}, phase2={phase2.GetType().Name}");

        CacheBuildResult buildResult;
        try
        {
            Phase1Result phase1Result = default;
            if (!resumed)
            {
                phase1Result = phase1Filter != null
                    ? phase1.RunFiltered(adapter, phase1Filter, ct)
                    : phase1.Run(adapter, ct);

                if (config.EnableCheckpoint && !string.IsNullOrWhiteSpace(checkpointDir))
                    _checkpointSerializer.Save(checkpointDir, navTypeName, adapter, phaseCompleted: 1);
            }
            else
            {
                _logger.LogInfo("[CacheRebuild] Phase 1 SKIPPED (resumed from checkpoint)");
            }

            var phase2Result = phase2.Run(adapter, ct);
            buildResult = new CacheBuildResult(phase1Result, phase2Result, cancelled: false, smokeResult);

            if (config.EnableCheckpoint && !string.IsNullOrWhiteSpace(checkpointDir))
                _checkpointSerializer.Delete(checkpointDir, navTypeName);

            if (!string.IsNullOrWhiteSpace(snapshotPath))
                _snapshotStore.Save(snapshotPath, adapter);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[CacheRebuild] BUILD CANCELLED");
            buildResult = new CacheBuildResult(default, default, cancelled: true, smokeResult);
        }

        WriteValidationReport(adapter, buildResult, config, mode);
        overallSw.Stop();
        LogFinalSummary(mode, navTypeName, settlementCount, effectiveParallelism, buildResult, overallSw.Elapsed);
        return buildResult;
    }

    private void LogConfigSummary(CacheRebuildConfig config)
    {
        _logger.LogDebug(
            $"[CacheRebuild] config: enabled={config.Enabled}, forceVanilla={config.ForceVanilla}, " +
            $"parallelism={config.Parallelism}, enableCheckpoint={config.EnableCheckpoint}, " +
            $"enableIncremental={config.EnableIncremental}, incrementalMaxChanged={config.IncrementalMaxChanged}, " +
            $"smokeTestPairs={config.SmokeTestPairs}, smokeTestTolerance={config.SmokeTestDistanceTolerance:E2}");
    }

    private void LogDiffDetails(SettlementDiff diff)
    {
        const int IdSampleLimit = 10;
        _logger.LogInfo($"[CacheRebuild] settlement diff: {diff.Added.Count} added, {diff.Moved.Count} moved, {diff.Removed.Count} removed");
        if (diff.Added.Count > 0)
            _logger.LogInfo($"[CacheRebuild]   added: {FormatIdSample(diff.Added, IdSampleLimit)}");
        if (diff.Moved.Count > 0)
            _logger.LogInfo($"[CacheRebuild]   moved: {FormatIdSample(diff.Moved, IdSampleLimit)}");
        if (diff.Removed.Count > 0)
            _logger.LogInfo($"[CacheRebuild]   removed: {FormatIdSample(diff.Removed, IdSampleLimit)}");
    }

    private static string FormatIdSample(System.Collections.Generic.HashSet<string> ids, int limit)
    {
        var sample = new System.Collections.Generic.List<string>();
        var more = 0;
        foreach (var id in ids)
        {
            if (sample.Count < limit) sample.Add(id);
            else more++;
        }
        var joined = string.Join(", ", sample);
        return more > 0 ? $"{joined}, …(+{more} more)" : joined;
    }

    private void LogFinalSummary(string mode, string navType, int settlements, int parallelism, CacheBuildResult result, TimeSpan totalElapsed)
    {
        BannerLogger.LogBanner(_logger, result.Cancelled ? "CACHE REBUILD CANCELLED" : "CACHE REBUILD COMPLETE");
        _logger.LogInfo(
            $"[CacheRebuild] SUMMARY mode={mode} nav={navType} settlements={settlements} parallelism={parallelism} " +
            $"phase1={ProgressLogger.FormatDuration(TimeSpan.FromSeconds(result.Phase1.ElapsedSeconds))} " +
            $"({result.Phase1.PairsComputed} pairs) " +
            $"phase2={ProgressLogger.FormatDuration(TimeSpan.FromSeconds(result.Phase2.ElapsedSeconds))} " +
            $"({result.Phase2.NeighborPairsAdded} neighbors) " +
            $"total={ProgressLogger.FormatDuration(totalElapsed)} " +
            $"smokeTest={result.SmokeTest.Outcome} cancelled={result.Cancelled}");
    }

    private string ResolveCheckpointDir(CacheRebuildConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.CheckpointRelativeDirectory))
            return "";
        return Path.GetFullPath(Path.Combine(_pathService.ModuleRootPath, "..", config.CheckpointRelativeDirectory));
    }

    private string ResolveSnapshotPath(CacheRebuildConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.SettlementSnapshotRelativePath))
            return "";
        return Path.GetFullPath(Path.Combine(_pathService.ModuleRootPath, "..", config.SettlementSnapshotRelativePath));
    }

    private static string GetFinalCachePath(string checkpointDir, string navType) =>
        Path.Combine(checkpointDir, $"settlements_distance_cache_{navType}.bin");

    private void WriteValidationReport(INavigationCacheAdapter adapter, CacheBuildResult result, CacheRebuildConfig config, string mode = "full")
    {
        if (string.IsNullOrWhiteSpace(config.ValidationReportRelativePath))
            return;

        try
        {
            var settlements = adapter.GetAllRegisteredSettlements();
            var fortifications = settlements.Count(s => s.IsFortification);

            var report = new ValidationReport
            {
                Timestamp = DateTime.UtcNow,
                Mode = mode,
                DurationSeconds = result.TotalSeconds,
                Cancelled = result.Cancelled,
                SettlementsTotal = settlements.Count,
                FortificationsTotal = fortifications,
                NavigationType = adapter.NavigationType.ToString(),
                Phase1 = new PhaseReport
                {
                    DurationSeconds = result.Phase1.ElapsedSeconds,
                    PairsComputed = result.Phase1.PairsComputed,
                },
                Phase2 = new PhaseReport
                {
                    DurationSeconds = result.Phase2.ElapsedSeconds,
                    NeighborPairsAdded = result.Phase2.NeighborPairsAdded,
                    FortificationsConsidered = result.Phase2.FortificationsConsidered,
                },
                SmokeTest = new SmokeTestReportData
                {
                    Outcome = result.SmokeTest.Outcome.ToString(),
                    PairsTested = result.SmokeTest.PairsTested,
                    MaxDistanceDelta = result.SmokeTest.MaxDistanceDelta,
                    Reason = result.SmokeTest.Reason,
                },
            };

            var basePath = _pathService.ModuleRootPath;
            var resolvedPath = Path.GetFullPath(Path.Combine(basePath, "..", config.ValidationReportRelativePath));
            _reportWriter.Write(resolvedPath, report);
        }
        catch (Exception ex)
        {
            _logger.LogError($"CacheBuilderService: failed to write validation report: {ex.Message}");
        }
    }

    private IPhase1Builder SelectPhase1(int effectiveParallelism) =>
        effectiveParallelism > 1 ? (IPhase1Builder)_parallelPhase1 : _serialPhase1;

    private IPhase2Builder SelectPhase2(int effectiveParallelism) =>
        effectiveParallelism > 1 ? (IPhase2Builder)_parallelPhase2 : _serialPhase2;
}
