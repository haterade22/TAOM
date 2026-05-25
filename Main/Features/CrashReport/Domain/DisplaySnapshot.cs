namespace TAOM.Features.CrashReport.Domain;

public sealed record DisplaySnapshot(
    int ResolutionWidth,
    int ResolutionHeight,
    int RefreshRate,
    bool IsFullscreen,
    int MonitorCount);
