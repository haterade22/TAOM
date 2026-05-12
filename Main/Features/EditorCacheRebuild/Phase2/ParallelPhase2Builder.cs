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

        BannerLogger.LogBanner(_logger, $"PHASE 2 START (parallel x{parallelism})");
        _logger.LogInfo($"[CacheRebuild] Phase2: fortifications={items.Count}, parallelism={parallelism}");

        // ConcurrentQueue has cheaper enumeration than ConcurrentBag (single-threaded post-loop flush).
        var neighborPairs = new ConcurrentQueue<(ISettlementDataHolder, ISettlementDataHolder)>();
        var progress = new ProgressLogger(_logger, "Phase2", items.Count, everyN: 5);

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
                    if (adapter.CheckBeingNeighbor(fortifications, s1, s2))
                        neighborPairs.Enqueue((s1, s2));
                }

                progress.Tick();
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[CacheRebuild] Phase2 CANCELLED during compute");
            throw;
        }

        _logger.LogInfo($"[CacheRebuild] Phase2: compute done, applying {neighborPairs.Count} neighbor relationships");
        var added = 0;
        foreach (var (a, b) in neighborPairs)
        {
            ct.ThrowIfCancellationRequested();
            adapter.AddNeighbor(a, b);
            added++;
        }

        sw.Stop();
        var result = new Phase2Result(items.Count, added, sw.Elapsed.TotalSeconds);
        _logger.LogInfo($"[CacheRebuild] Phase2 DONE: {added} neighbor pairs across {items.Count} fortifications in {ProgressLogger.FormatDuration(sw.Elapsed)} (parallelism={parallelism})");
        BannerLogger.LogBanner(_logger, "PHASE 2 END");
        return result;
    }
}
