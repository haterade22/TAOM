namespace TAOM.Features.ArmyTargeting.Diagnostics;

/// <summary>
/// Records AI-army gathering dead ends (the vanilla NRE Patch49 swallows) as a reviewable log so
/// broken sieges can be found and fixed. Classifies the failure, deduplicates by
/// <c>(kingdom, focus settlement)</c>, and emits one detailed WARNING per distinct problem siege.
/// </summary>
public interface ISiegeGatheringDiagnosticsService
{
    /// <summary>
    /// Classify + record one gathering failure. First occurrence of a
    /// <c>(kingdomId, focusSettlementId)</c> pair logs the full detail at WARNING; later repeats
    /// increment a counter and log a terse DEBUG line so WARNINGs never spam.
    /// </summary>
    void Record(SiegeGatheringFailureInfo info);

    /// <summary>Pure classification of a failure from its census counts. Exposed for testing.</summary>
    SiegeGatheringFailureReason Classify(SiegeGatheringFailureInfo info);

    /// <summary>Pure human-readable one-line rendering of a failure. Exposed for testing.</summary>
    string Format(SiegeGatheringFailureInfo info, SiegeGatheringFailureReason reason);
}
