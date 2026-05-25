using System.Collections.Generic;

namespace TAOM.Features.CrashReport.Domain;

// Captured projection of a System.Exception with the inner chain pre-walked.
// Depth-capped at construction time (10 by default) to defend against cyclical
// or pathologically deep chains.
public sealed record ExceptionFrame(
    string Type,
    string Message,
    string? Source,
    int HResult,
    string? TargetSiteFullName,
    string? StackTrace,
    IReadOnlyList<DataEntry> Data,
    ExceptionFrame? InnerException);

// Exception.Data dictionary entries — some mods stash diagnostic context here
// (e.g., ButterLib attaches its log path). Stringified safely.
public sealed record DataEntry(string Key, string Value);
