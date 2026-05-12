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
        _logger.LogInfo($"[SmokeTestGate] START — parallelism={config.Parallelism}, configuredPairs={config.SmokeTestPairs}, tolerance={config.SmokeTestDistanceTolerance:E2}");

        if (config.Parallelism <= 1)
        {
            _logger.LogInfo("[SmokeTestGate] SKIPPED — parallelism=1, no need to verify parallel safety");
            return new SmokeTestResult(SmokeTestOutcome.Skipped, 0, 0, "parallelism=1; smoke test not required");
        }

        var settlements = adapter.GetAllRegisteredSettlements();
        var fortifications = settlements.Where(s => s.IsFortification).ToList();
        _logger.LogInfo($"[SmokeTestGate] candidate pool: {fortifications.Count} fortifications (out of {settlements.Count} total settlements)");

        if (fortifications.Count < 2)
        {
            _logger.LogWarning($"[SmokeTestGate] SKIPPED — only {fortifications.Count} fortifications available, need >= 2");
            return new SmokeTestResult(SmokeTestOutcome.Skipped, 0, 0, "fewer than 2 fortifications available");
        }

        var pairs = PickRandomPairs(fortifications, config.SmokeTestPairs);
        if (pairs.Count == 0)
        {
            _logger.LogWarning("[SmokeTestGate] SKIPPED — could not generate any distinct pairs");
            return new SmokeTestResult(SmokeTestOutcome.Skipped, 0, 0, "no distinct pairs generated");
        }
        _logger.LogInfo($"[SmokeTestGate] generated {pairs.Count} distinct test pairs (seed={RandomSeed})");

        var serialSw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInfo($"[SmokeTestGate] running {pairs.Count} pairs in SERIAL baseline");
        var baseline = new float[pairs.Count];
        for (int i = 0; i < pairs.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (s1, s2) = pairs[i];
            var result = adapter.ComputeClosestEntrancePair(s1, false, s2, false);
            baseline[i] = result.IsValid ? result.Distance : 0f;
        }
        serialSw.Stop();
        _logger.LogInfo($"[SmokeTestGate] serial baseline computed in {serialSw.ElapsedMilliseconds}ms (avg {serialSw.ElapsedMilliseconds / (double)pairs.Count:F1}ms/pair)");

        var parallelSw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInfo($"[SmokeTestGate] running {pairs.Count} pairs in PARALLEL x{config.Parallelism}");
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
        parallelSw.Stop();
        var speedupFactor = serialSw.ElapsedMilliseconds > 0 ? serialSw.ElapsedMilliseconds / (double)parallelSw.ElapsedMilliseconds : 0;
        _logger.LogInfo($"[SmokeTestGate] parallel run completed in {parallelSw.ElapsedMilliseconds}ms (speedup={speedupFactor:F2}x)");

        var maxDelta = 0f;
        var worstPairIndex = -1;
        var totalDivergence = 0.0;
        for (int i = 0; i < pairs.Count; i++)
        {
            var delta = Math.Abs(parallelResults[i] - baseline[i]);
            totalDivergence += delta;
            if (delta > maxDelta)
            {
                maxDelta = delta;
                worstPairIndex = i;
            }
        }
        var avgDelta = totalDivergence / pairs.Count;

        if (worstPairIndex >= 0)
        {
            var (ws1, ws2) = pairs[worstPairIndex];
            _logger.LogInfo(
                $"[SmokeTestGate] worst pair: {ws1.StringId}↔{ws2.StringId} " +
                $"serial={baseline[worstPairIndex]:F4} parallel={parallelResults[worstPairIndex]:F4} delta={maxDelta:E2}, avg delta across all pairs={avgDelta:E2}");
        }

        if (maxDelta > config.SmokeTestDistanceTolerance)
        {
            _logger.LogError(
                $"[SmokeTestGate] **** FAILED **** — max delta {maxDelta:E2} > tolerance {config.SmokeTestDistanceTolerance:E2} " +
                $"across {pairs.Count} pairs. Native pathfinder is NOT parallel-safe on this configuration. Falling back to serial mode (~6-8x slower).");
            return new SmokeTestResult(SmokeTestOutcome.Failed, pairs.Count, maxDelta,
                $"parallel pathfind diverged from serial by {maxDelta:E2}");
        }

        _logger.LogInfo(
            $"[SmokeTestGate] **** PASSED **** — max delta {maxDelta:E2} <= tolerance {config.SmokeTestDistanceTolerance:E2} " +
            $"across {pairs.Count} pairs. Parallel mode is SAFE. Proceeding with parallelism={config.Parallelism}.");
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
