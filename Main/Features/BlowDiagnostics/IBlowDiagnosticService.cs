using TAOM.Features.BlowDiagnostics.Domain;

namespace TAOM.Features.BlowDiagnostics;

// Durable `[BlowDiag]` stamps for the blow / death / siege-shot path. Toggle-gated (off by
// default) and shipped to capture an intermittent NATIVE AV that leaves no managed stack: the
// stamps go through IModLogger's durable (synchronous-flush) level so the LAST line on disk
// names the fatal blow even when the process is killed mid-frame. See docs/features/blow-diagnostics.md.
public interface IBlowDiagnosticService
{
    // Read by the hot-path Harmony prefixes BEFORE they extract any fields, so a disabled
    // build costs one bool read per blow.
    bool IsEnabled { get; }

    void LogBlow(BlowDiagRecord record);
    void LogDeath(BlowDiagRecord record);
    void LogSiegeShot(string missileItemId, string side);
}
