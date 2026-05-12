using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild.Progress;

namespace TAOM.Features.EditorCacheRebuild.Phase1;

public class ParallelPhase1Builder : IPhase1Builder
{
    private readonly IModLogger _logger;
    private readonly ICacheRebuildConfigProvider _configProvider;

    public ParallelPhase1Builder(IModLogger logger, ICacheRebuildConfigProvider configProvider)
    {
        _logger = logger;
        _configProvider = configProvider;
    }

    public virtual Phase1Result Run(INavigationCacheAdapter adapter, CancellationToken ct) =>
        RunCore(adapter, filter: null, ct);

    public virtual Phase1Result RunFiltered(INavigationCacheAdapter adapter, IPhase1Filter filter, CancellationToken ct) =>
        RunCore(adapter, filter, ct);

    private Phase1Result RunCore(INavigationCacheAdapter adapter, IPhase1Filter? filter, CancellationToken ct)
    {
        var config = _configProvider.GetConfig();
        var parallelism = config.Parallelism;
        var settlements = adapter.GetAllRegisteredSettlements();
        var navType = adapter.NavigationType;
        var pairsComputed = 0;
        var sw = Stopwatch.StartNew();
        var mode = filter != null ? "incremental" : "full";
        var memBefore = GC.GetTotalMemory(forceFullCollection: false);

        BannerLogger.LogBanner(_logger, $"PHASE 1 START (parallel x{parallelism}, {mode})");
        var totalPossiblePairs = (long)settlements.Count * (settlements.Count - 1) / 2;
        _logger.LogInfo($"[CacheRebuild] Phase1: NavigationType={navType}, settlements={settlements.Count}, totalPossiblePairs={totalPossiblePairs:N0}, parallelism={parallelism}, mode={mode}, mem={FormatMB(memBefore)}");

        // ConcurrentQueue has cheaper enumeration than ConcurrentBag (single-threaded post-loop flush).
        var buffer = new ConcurrentQueue<PairComputeResult>();
        var progress = new ProgressLogger(_logger, "Phase1", settlements.Count, everyN: 25);
        var firstPairLogged = 0;
        var firstPairSw = Stopwatch.StartNew();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = parallelism,
            CancellationToken = ct,
        };

        try
        {
            Parallel.For(0, settlements.Count, options, i =>
            {
                var s1 = settlements[i];
                for (int j = i + 1; j < settlements.Count; j++)
                {
                    var s2 = settlements[j];
                    if (filter != null && !filter.ShouldComputePair(s1, s2)) continue;
                    var added = ComputePairsForNavigationType(adapter, navType, s1, s2, buffer);
                    Interlocked.Add(ref pairsComputed, added);

                    // Liveness heartbeat — log the FIRST pair to confirm pathfinder is reachable
                    // from a worker thread (catches "engine pathfinder threw on background thread"
                    // failures within milliseconds, not after the first progress tick at i=25).
                    if (added > 0 && Interlocked.CompareExchange(ref firstPairLogged, 1, 0) == 0)
                    {
                        _logger.LogInfo($"[CacheRebuild] Phase1: FIRST PAIR computed in {firstPairSw.ElapsedMilliseconds}ms — pathfinder is reachable from worker threads. Continuing...");
                    }
                }

                progress.Tick();
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[CacheRebuild] Phase1 CANCELLED during compute");
            throw;
        }
        catch (AggregateException agg)
        {
            // Parallel.For wraps inner exceptions — unwrap so the root cause is visible in the log.
            _logger.LogError($"[CacheRebuild] Phase1 FAILED via AggregateException with {agg.InnerExceptions.Count} inner exception(s)");
            for (int k = 0; k < agg.InnerExceptions.Count; k++)
            {
                var inner = agg.InnerExceptions[k];
                _logger.LogError($"[CacheRebuild]   inner[{k}]: {inner.GetType().FullName}: {inner.Message}\n{inner.StackTrace}");
            }
            throw;
        }

        var flushSw = Stopwatch.StartNew();
        _logger.LogInfo($"[CacheRebuild] Phase1: compute done in {ProgressLogger.FormatDuration(sw.Elapsed)}, flushing buffered results to cache (serial)");
        var flushedCount = 0;
        foreach (var result in buffer)
        {
            ct.ThrowIfCancellationRequested();
            adapter.WriteComputedPair(in result);
            flushedCount++;
        }
        flushSw.Stop();
        _logger.LogInfo($"[CacheRebuild] Phase1: flush wrote {flushedCount:N0} pairs to vanilla dict in {flushSw.ElapsedMilliseconds}ms");

        sw.Stop();
        var memAfter = GC.GetTotalMemory(forceFullCollection: false);
        var phaseResult = new Phase1Result(pairsComputed, sw.Elapsed.TotalSeconds);
        _logger.LogInfo($"[CacheRebuild] Phase1 DONE: {pairsComputed:N0} entrance-pairs in {ProgressLogger.FormatDuration(sw.Elapsed)} ({mode}, parallelism={parallelism}, mem-delta={FormatMB(memAfter - memBefore)})");
        BannerLogger.LogBanner(_logger, "PHASE 1 END");
        return phaseResult;
    }

    private static string FormatMB(long bytes)
    {
        var sign = bytes < 0 ? "-" : "";
        var abs = Math.Abs(bytes);
        return $"{sign}{abs / (1024.0 * 1024):F1}MB";
    }

    private static int ComputePairsForNavigationType(
        INavigationCacheAdapter adapter,
        MobileParty.NavigationType navType,
        ISettlementDataHolder s1,
        ISettlementDataHolder s2,
        ConcurrentQueue<PairComputeResult> buffer)
    {
        switch (navType)
        {
            case MobileParty.NavigationType.Default:
                BufferPair(adapter, s1, false, s2, false, buffer);
                return 1;

            case MobileParty.NavigationType.Naval:
                if (s1.HasPort && s2.HasPort)
                {
                    BufferPair(adapter, s1, true, s2, true, buffer);
                    return 1;
                }
                return 0;

            case MobileParty.NavigationType.All:
                var added = 0;
                BufferPair(adapter, s1, false, s2, false, buffer);
                added++;
                if (s1.HasPort && s2.HasPort)
                {
                    BufferPair(adapter, s1, true, s2, true, buffer);
                    added++;
                }
                if (s2.HasPort)
                {
                    BufferPair(adapter, s1, false, s2, true, buffer);
                    added++;
                }
                if (s1.HasPort)
                {
                    BufferPair(adapter, s1, true, s2, false, buffer);
                    added++;
                }
                return added;

            default:
                return 0;
        }
    }

    private static void BufferPair(
        INavigationCacheAdapter adapter,
        ISettlementDataHolder s1, bool isPort1,
        ISettlementDataHolder s2, bool isPort2,
        ConcurrentQueue<PairComputeResult> buffer)
    {
        var result = adapter.ComputeClosestEntrancePair(s1, isPort1, s2, isPort2);
        if (result.IsValid)
            buffer.Enqueue(result);
    }
}
