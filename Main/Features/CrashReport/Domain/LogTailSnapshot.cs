using System.Collections.Generic;

namespace TAOM.Features.CrashReport.Domain;

/// <param name="DiagLogPath">
/// TAOM.Dependencies/diag.log. Written on every session (Dependencies logs its install sequence
/// unconditionally), and it is where PatchShield records the MissingMethod / MissingField /
/// TypeLoad trinity that signals a build running against a Bannerlord it was not compiled for. It
/// lives in the Dependencies module folder rather than Logs/, so it is a separate file from the
/// taom_debug log, and it was absent from the bundle until 2026-08-19: the most diagnostic artifact
/// for an engine mismatch was never the one players uploaded. An EMPTY or missing diag section is
/// therefore itself a signal (module dir unresolved, or TAOM.Dependencies never loaded).
/// </param>
public sealed record LogTailSnapshot(
    string? TaomDebugLogPath,
    IReadOnlyList<string> TaomDebugLogTail,
    string? RglLogPath,
    IReadOnlyList<string> RglLogTail,
    string? DiagLogPath,
    IReadOnlyList<string> DiagLogTail);
