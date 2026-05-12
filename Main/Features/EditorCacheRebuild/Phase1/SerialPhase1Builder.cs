using System.Diagnostics;
using System.Threading;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild.Progress;

namespace TAOM.Features.EditorCacheRebuild.Phase1;

public class SerialPhase1Builder : IPhase1Builder
{
    private readonly IModLogger _logger;

    public SerialPhase1Builder(IModLogger logger)
    {
        _logger = logger;
    }

    public virtual Phase1Result Run(INavigationCacheAdapter adapter, CancellationToken ct) =>
        RunCore(adapter, filter: null, ct);

    public virtual Phase1Result RunFiltered(INavigationCacheAdapter adapter, IPhase1Filter filter, CancellationToken ct) =>
        RunCore(adapter, filter, ct);

    private Phase1Result RunCore(INavigationCacheAdapter adapter, IPhase1Filter? filter, CancellationToken ct)
    {
        var settlements = adapter.GetAllRegisteredSettlements();
        var navType = adapter.NavigationType;
        var pairsComputed = 0;
        var sw = Stopwatch.StartNew();
        var mode = filter != null ? "incremental" : "full";

        BannerLogger.LogBanner(_logger, $"PHASE 1 START (serial, {mode})");
        _logger.LogInfo($"[CacheRebuild] Phase1: NavigationType={navType}, settlements={settlements.Count}");

        var progress = new ProgressLogger(_logger, "Phase1", settlements.Count, everyN: 50);

        for (int i = 0; i < settlements.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var s1 = settlements[i];
            for (int j = i + 1; j < settlements.Count; j++)
            {
                var s2 = settlements[j];
                if (filter != null && !filter.ShouldComputePair(s1, s2)) continue;
                pairsComputed += AddPairsForNavigationType(adapter, navType, s1, s2);
            }

            progress.Tick();
        }

        sw.Stop();
        var result = new Phase1Result(pairsComputed, sw.Elapsed.TotalSeconds);
        _logger.LogInfo($"[CacheRebuild] Phase1 DONE: {pairsComputed} entrance-pairs in {ProgressLogger.FormatDuration(sw.Elapsed)} ({mode})");
        BannerLogger.LogBanner(_logger, "PHASE 1 END");
        return result;
    }

    private static int AddPairsForNavigationType(
        INavigationCacheAdapter adapter,
        MobileParty.NavigationType navType,
        ISettlementDataHolder s1,
        ISettlementDataHolder s2)
    {
        switch (navType)
        {
            case MobileParty.NavigationType.Default:
                adapter.AddClosestEntrancePair(s1, false, s2, false);
                return 1;

            case MobileParty.NavigationType.Naval:
                if (s1.HasPort && s2.HasPort)
                {
                    adapter.AddClosestEntrancePair(s1, true, s2, true);
                    return 1;
                }
                return 0;

            case MobileParty.NavigationType.All:
                var added = 0;
                adapter.AddClosestEntrancePair(s1, false, s2, false);
                added++;
                if (s1.HasPort && s2.HasPort)
                {
                    adapter.AddClosestEntrancePair(s1, true, s2, true);
                    added++;
                }
                if (s2.HasPort)
                {
                    adapter.AddClosestEntrancePair(s1, false, s2, true);
                    added++;
                }
                if (s1.HasPort)
                {
                    adapter.AddClosestEntrancePair(s1, true, s2, false);
                    added++;
                }
                return added;

            default:
                return 0;
        }
    }
}
