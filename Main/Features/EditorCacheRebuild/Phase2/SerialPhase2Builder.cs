using System.Diagnostics;
using System.Threading;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild.Progress;

namespace TAOM.Features.EditorCacheRebuild.Phase2;

public class SerialPhase2Builder : IPhase2Builder
{
    private readonly IModLogger _logger;

    public SerialPhase2Builder(IModLogger logger)
    {
        _logger = logger;
    }

    public virtual Phase2Result Run(INavigationCacheAdapter adapter, CancellationToken ct)
    {
        var fortifications = adapter.GetFortificationsForNeighborDetection();
        var neighborsAdded = 0;
        var sw = Stopwatch.StartNew();

        BannerLogger.LogBanner(_logger, "PHASE 2 START (serial)");
        _logger.LogInfo($"[CacheRebuild] Phase2: fortifications={fortifications.Items.Count}");

        var progress = new ProgressLogger(_logger, "Phase2", fortifications.Items.Count, everyN: 5);

        for (int i = 0; i < fortifications.Items.Count - 1; i++)
        {
            ct.ThrowIfCancellationRequested();

            var s1 = fortifications.Items[i];
            if (!s1.IsFortification) { progress.Tick(); continue; }

            for (int j = i + 1; j < fortifications.Items.Count; j++)
            {
                var s2 = fortifications.Items[j];
                if (!s2.IsFortification) continue;

                if (adapter.CheckBeingNeighbor(fortifications, s1, s2))
                {
                    adapter.AddNeighbor(s1, s2);
                    neighborsAdded++;
                }
            }

            progress.Tick();
        }

        sw.Stop();
        var result = new Phase2Result(fortifications.Items.Count, neighborsAdded, sw.Elapsed.TotalSeconds);
        _logger.LogInfo($"[CacheRebuild] Phase2 DONE: {neighborsAdded} neighbor pairs across {fortifications.Items.Count} fortifications in {ProgressLogger.FormatDuration(sw.Elapsed)}");
        BannerLogger.LogBanner(_logger, "PHASE 2 END");
        return result;
    }
}
