using System.Globalization;

namespace TAOM.Features.MissionPerf;

/// <summary>
/// The one line format, kept pure so the heartbeat behavior stays a thin reader of the engine
/// and the parser in <c>tools/</c> has a single fixture to match.
/// </summary>
public static class MissionPerfLine
{
    public static string Build(double tSeconds, FrameWindow window, int agents, int activeAgents, int formations, int gc0, int gc1, int gc2)
    {
        return string.Format(CultureInfo.InvariantCulture,
            "[MissionPerf] t=+{0:0}s frames={1} fps={2:0.0} avgMs={3:0.00} p95Ms={4:0.00} maxMs={5:0.0} agents={6} active={7} formations={8} gc0={9} gc1={10} gc2={11}",
            tSeconds, window.Frames, window.Fps, window.AverageMs, window.P95Ms, window.MaxMs,
            agents, activeAgents, formations, gc0, gc1, gc2);
    }
}
