using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.EditorCacheRebuild.Validation;

public class SmokeTestGate : ISmokeTestGate
{
    private const int RandomSeed = 42;

    private readonly IModLogger _logger;
    private readonly ICacheRebuildConfigProvider _configProvider;

    public SmokeTestGate(IModLogger logger, ICacheRebuildConfigProvider configProvider)
    {
        _logger = logger;
        _configProvider = configProvider;
    }

    public SmokeTestResult Run(INavigationCacheAdapter adapter, CancellationToken ct)
    {
        var config = _configProvider.GetConfig();
        if (config.Parallelism <= 1)
        {
            return new SmokeTestResult(SmokeTestOutcome.Skipped, 0, 0, "parallelism=1; smoke test not required");
        }

        var settlements = adapter.GetAllRegisteredSettlements();
        var fortifications = settlements.Where(s => s.IsFortification).ToList();
        if (fortifications.Count < 2)
        {
            return new SmokeTestResult(SmokeTestOutcome.Skipped, 0, 0, "fewer than 2 fortifications available");
        }

        var pairs = PickRandomPairs(fortifications, config.SmokeTestPairs);
        if (pairs.Count == 0)
        {
            return new SmokeTestResult(SmokeTestOutcome.Skipped, 0, 0, "no distinct pairs generated");
        }

        _logger.LogDebug($"[SmokeTestGate] running {pairs.Count} pairs in serial baseline");
        var baseline = new float[pairs.Count];
        for (int i = 0; i < pairs.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (s1, s2) = pairs[i];
            var result = adapter.ComputeClosestEntrancePair(s1, false, s2, false);
            baseline[i] = result.IsValid ? result.Distance : 0f;
        }

        _logger.LogDebug($"[SmokeTestGate] running {pairs.Count} pairs in parallel x{config.Parallelism}");
        var parallelResults = new float[pairs.Count];
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = config.Parallelism,
            CancellationToken = ct,
        };

        Parallel.For(0, pairs.Count, options, i =>
        {
            var (s1, s2) = pairs[i];
            var result = adapter.ComputeClosestEntrancePair(s1, false, s2, false);
            parallelResults[i] = result.IsValid ? result.Distance : 0f;
        });

        var maxDelta = 0f;
        var worstPairIndex = -1;
        for (int i = 0; i < pairs.Count; i++)
        {
            var delta = Math.Abs(parallelResults[i] - baseline[i]);
            if (delta > maxDelta)
            {
                maxDelta = delta;
                worstPairIndex = i;
            }
        }

        if (worstPairIndex >= 0 && maxDelta > 0)
        {
            var (ws1, ws2) = pairs[worstPairIndex];
            _logger.LogDebug(
                $"[SmokeTestGate] worst pair: {ws1.StringId}↔{ws2.StringId} " +
                $"serial={baseline[worstPairIndex]:F4} parallel={parallelResults[worstPairIndex]:F4} delta={maxDelta:F6}");
        }

        if (maxDelta > config.SmokeTestDistanceTolerance)
        {
            _logger.LogWarning(
                $"[SmokeTestGate] FAILED — max delta {maxDelta:F6} > tolerance {config.SmokeTestDistanceTolerance:F6} " +
                $"across {pairs.Count} pairs. Native pathfinder may not be parallel-safe on this setup. Falling back to serial mode.");
            return new SmokeTestResult(SmokeTestOutcome.Failed, pairs.Count, maxDelta,
                $"parallel pathfind diverged from serial by {maxDelta:F6}");
        }

        _logger.LogInfo(
            $"[SmokeTestGate] PASSED — max delta {maxDelta:F6} <= tolerance {config.SmokeTestDistanceTolerance:F6} " +
            $"across {pairs.Count} pairs. Parallel mode is safe.");
        return new SmokeTestResult(SmokeTestOutcome.Passed, pairs.Count, maxDelta);
    }

    private static List<(ISettlementDataHolder, ISettlementDataHolder)> PickRandomPairs(
        IReadOnlyList<ISettlementDataHolder> fortifications,
        int desiredCount)
    {
        var rng = new Random(RandomSeed);
        var pairs = new List<(ISettlementDataHolder, ISettlementDataHolder)>();
        var seen = new HashSet<(string, string)>();
        var maxAttempts = desiredCount * 4;

        for (int attempt = 0; attempt < maxAttempts && pairs.Count < desiredCount; attempt++)
        {
            var i = rng.Next(fortifications.Count);
            var j = rng.Next(fortifications.Count);
            if (i == j) continue;
            var s1 = fortifications[i];
            var s2 = fortifications[j];
            var key = string.CompareOrdinal(s1.StringId, s2.StringId) < 0
                ? (s1.StringId, s2.StringId)
                : (s2.StringId, s1.StringId);
            if (!seen.Add(key)) continue;
            pairs.Add((s1, s2));
        }

        return pairs;
    }
}
