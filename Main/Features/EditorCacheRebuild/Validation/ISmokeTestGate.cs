using System.Threading;
using TAOM.Adapters;

namespace TAOM.Features.EditorCacheRebuild.Validation;

public interface ISmokeTestGate
{
    SmokeTestResult Run(INavigationCacheAdapter adapter, CancellationToken ct);
}

public readonly struct SmokeTestResult
{
    public readonly SmokeTestOutcome Outcome;
    public readonly int PairsTested;
    public readonly float MaxDistanceDelta;
    public readonly string? Reason;

    public SmokeTestResult(SmokeTestOutcome outcome, int pairsTested, float maxDistanceDelta, string? reason = null)
    {
        Outcome = outcome;
        PairsTested = pairsTested;
        MaxDistanceDelta = maxDistanceDelta;
        Reason = reason;
    }

    public bool IsSafeForParallel => Outcome == SmokeTestOutcome.Passed || Outcome == SmokeTestOutcome.Skipped;
}

public enum SmokeTestOutcome
{
    Passed,
    Failed,
    Skipped,
}
