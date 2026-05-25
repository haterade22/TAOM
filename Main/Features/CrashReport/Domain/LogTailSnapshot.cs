using System.Collections.Generic;

namespace TAOM.Features.CrashReport.Domain;

public sealed record LogTailSnapshot(
    string? TaomDebugLogPath,
    IReadOnlyList<string> TaomDebugLogTail,
    string? RglLogPath,
    IReadOnlyList<string> RglLogTail);
