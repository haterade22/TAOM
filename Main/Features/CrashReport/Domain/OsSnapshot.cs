namespace TAOM.Features.CrashReport.Domain;

public sealed record OsSnapshot(
    string OsDescription,
    string OsVersion,
    bool Is64BitOs,
    int LogicalProcessorCount,
    string ArchitectureLabel,
    string CurrentCulture,
    string CurrentUiCulture,
    string ClrVersion);
