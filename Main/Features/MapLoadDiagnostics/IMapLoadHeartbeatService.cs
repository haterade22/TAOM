using System;

namespace TAOM.Features.MapLoadDiagnostics;

public interface IMapLoadHeartbeatService
{
    /// <summary>
    /// Called every campaign frame with the time the tick took. Returns true when a heartbeat is
    /// due, so the caller can build the (expensive) census only on those frames.
    /// </summary>
    bool ShouldEmit(DateTime nowUtc, double tickMs);

    /// <summary>Mean tick milliseconds accumulated since the last emit.</summary>
    double TickMsAverage { get; }

    /// <summary>Formats the heartbeat and opens a new window. Call only when ShouldEmit returned true.</summary>
    string BuildLine(DateTime nowUtc, MapLoadSample sample);
}
