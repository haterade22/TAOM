using System.Collections.Generic;

namespace TAOM.Features.CrashReport.Domain;

public sealed record FrameTimingSnapshot(
    IReadOnlyList<float> RecentFrameTimesMs,
    double AverageFrameTimeMs,
    double FpsAtCrash,
    int SampleCount);
