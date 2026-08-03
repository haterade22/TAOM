using System.Collections.Generic;
using TAOM.Features.SiegePropDiagnostics.Models;

namespace TAOM.Features.SiegePropDiagnostics;

public interface ISiegePropDiagnosticsService
{
    /// <summary>
    /// Classify one prop snapshot. Pure — no engine access, no logging.
    /// </summary>
    SiegePropDiagnosis Diagnose(SiegePropSnapshot snapshot);

    /// <summary>
    /// Render the whole scene's props as log lines: one per prop plus a summary. Returns an empty
    /// list when the feature is off, so the caller need not re-check the toggle.
    /// </summary>
    IReadOnlyList<string> BuildReport(string sceneName, bool isSiegeBattle, IReadOnlyList<SiegePropSnapshot> snapshots);
}
