using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild.Progress;

namespace TAOM.Features.EditorCacheRebuild.Phase2;

public class ParallelPhase2Builder : IPhase2Builder
{
    private readonly IModLogger _logger;
    private readonly ICacheRebuildConfigProvider _configProvider;

    public ParallelPhase2Builder(IModLogger logger, ICacheRebuildConfigProvider configProvider)
    {
        _logger = logger;
        _configProvider = configProvider;
    }

    public virtual Phase2Result Run(INavigationCacheAdapter adapter, CancellationToken ct)
    {
        var config = _configProvider.GetConfig();
        var parallelism = config.Parallelism;
        var fortifications = adapter.GetFortificationsForNeighborDetection();
        var items = fortifications.Items;
        var sw = Stopwatch.StartNew();
        var memBefore = GC.GetTotalMemory(forceFullCollection: false);

        BannerLogger.LogBanner(_logger, $"PHASE 2 START (parallel x{parallelism})");
        var totalCheckCount = (long)items.Count * (items.Count - 1) / 2;
        _logger.LogInfo($"[CacheRebuild] Phase2: fortifications={items.Count}, possibleCheckPairs={totalCheckCount:N0}, parallelism={parallelism}, mem={FormatMB(memBefore)}");

        // ConcurrentQueue has cheaper enumeration than ConcurrentBag (single-threaded post-loop flush).
        var neighborPairs = new ConcurrentQueue<(ISettlementDataHolder, ISettlementDataHolder)>();
        var progress = new ProgressLogger(_logger, "Phase2", items.Count, everyN: 5);
        var firstNeighborLogged = 0;
        var firstCheckLogged = 0;
        var firstCheckSw = Stopwatch.StartNew();
        // Tracked alongside the Enqueue calls — avoids the O(N) ConcurrentQueue.Count traversal
        // when we want to log the pre-flush count.
        var enqueuedCount = 0;

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = parallelism,
            CancellationToken = ct,
        };

        try
        {
            Parallel.For(0, items.Count - 1, options, i =>
            {
                var s1 = items[i];
                if (!s1.IsFortification) { progress.Tick(); return; }

                for (int j = i + 1; j < items.Count; j++)
                {
                    var s2 = items[j];
                    if (!s2.IsFortification) continue;
                    var isNeighbor = adapter.CheckBeingNeighbor(fortifications, s1, s2);

                    // Liveness heartbeat — first neighbor-check confirms the corridor scan path is
                    // reachable from worker threads (Phase 2 stresses the pathfinder differently
                    // from Phase 1, so we want a separate confirmation).
                    if (Interlocked.CompareExchange(ref firstCheckLogged, 1, 0) == 0)
                    {
                        _logger.LogInfo($"[CacheRebuild] Phase2: FIRST CHECK completed in {firstCheckSw.ElapsedMilliseconds}ms (s1={s1.StringId}, s2={s2.StringId}, isNeighbor={isNeighbor})");
                    }

                    if (isNeighbor)
                    {
                        neighborPairs.Enqueue((s1, s2));
                        Interlocked.Increment(ref enqueuedCount);
                        if (Interlocked.CompareExchange(ref firstNeighborLogged, 1, 0) == 0)
                        {
                            _logger.LogInfo($"[CacheRebuild] Phase2: FIRST NEIGHBOR found: {s1.StringId} ↔ {s2.StringId}");
                        }
                    }
                }

                progress.Tick();
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[CacheRebuild] Phase2 CANCELLED during compute");
            throw;
        }
        catch (AggregateException agg)
        {
            _logger.LogError($"[CacheRebuild] Phase2 FAILED via AggregateException with {agg.InnerExceptions.Count} inner exception(s)");
            for (int k = 0; k < agg.InnerExceptions.Count; k++)
            {
                var inner = agg.InnerExceptions[k];
                _logger.LogError($"[CacheRebuild]   inner[{k}]: {inner.GetType().FullName}: {inner.Message}\n{inner.StackTrace}");
            }
            throw;
        }

        var flushSw = Stopwatch.StartNew();
        _logger.LogInfo($"[CacheRebuild] Phase2: compute done in {ProgressLogger.FormatDuration(sw.Elapsed)}, applying {enqueuedCount:N0} neighbor relationships (serial)");
        var added = 0;
        foreach (var (a, b) in neighborPairs)
        {
            ct.ThrowIfCancellationRequested();
            adapter.AddNeighbor(a, b);
            added++;
        }
        flushSw.Stop();
        _logger.LogInfo($"[CacheRebuild] Phase2: flush applied {added:N0} neighbor pairs in {flushSw.ElapsedMilliseconds}ms");

        sw.Stop();
        var memAfter = GC.GetTotalMemory(forceFullCollection: false);
        var result = new Phase2Result(items.Count, added, sw.Elapsed.TotalSeconds);
        _logger.LogInfo($"[CacheRebuild] Phase2 DONE: {added:N0} neighbor pairs across {items.Count} fortifications in {ProgressLogger.FormatDuration(sw.Elapsed)} (parallelism={parallelism}, mem-delta={FormatMB(memAfter - memBefore)})");
        BannerLogger.LogBanner(_logger, "PHASE 2 END");
        return result;
    }

    private static string FormatMB(long bytes)
    {
        var sign = bytes < 0 ? "-" : "";
        var abs = Math.Abs(bytes);
        return $"{sign}{abs / (1024.0 * 1024):F1}MB";
    }
}
