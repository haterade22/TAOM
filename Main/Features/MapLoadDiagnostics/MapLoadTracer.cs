using System;
using System.Diagnostics;
using System.Text;
using TAOM.Core.Logging;

namespace TAOM.Features.MapLoadDiagnostics;

/// <summary>
/// Shared sink for the lifecycle trace around campaign start and map load.
///
/// <para>
/// Written after several rounds of single-point logging each answered one hypothesis and cost a
/// full game launch. The heartbeat established that the map runs at 85 fps behind an overlay that
/// never lifts, so what is missing is not more sampling but the ORDER of events: which state was
/// pushed, who raised the loading window, and what never came back. Every event carries a monotonic
/// sequence and a millisecond offset so the log reads as a timeline rather than scattered lines.
/// </para>
///
/// <para>
/// Static and allocation-light on purpose: these fire from Harmony patches on engine lifecycle
/// methods, where resolving through IoC is forbidden. Everything is wrapped, because a tracer must
/// never be the thing that breaks the load it is tracing.
/// </para>
/// </summary>
public static class MapLoadTracer
{
    private static IModLogger _logger;
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static int _seq;

    public static void Initialize(IModLogger logger) => _logger = logger;

    /// <summary>Logs one lifecycle event. Never throws.</summary>
    public static void Trace(string evt, string detail = null)
    {
        var logger = _logger;
        if (logger == null) return;
        try
        {
            var n = System.Threading.Interlocked.Increment(ref _seq);
            var line = detail == null
                ? $"[MapLoad] #{n} t={Clock.ElapsedMilliseconds}ms {evt}"
                : $"[MapLoad] #{n} t={Clock.ElapsedMilliseconds}ms {evt} :: {detail}";
            logger.LogInfo(line);
        }
        catch { /* diagnostic only */ }
    }

    /// <summary>
    /// Logs an event together with its managed caller chain. Reserved for events that fire a
    /// handful of times, because building a stack trace is not free: the loading-window
    /// transitions are the whole question, and "who called this" is the answer we need.
    /// </summary>
    public static void TraceWithCallers(string evt, int frames = 10)
    {
        var logger = _logger;
        if (logger == null) return;
        try
        {
            var sb = new StringBuilder();
            // Skip frame 0 (this method) and frame 1 (the patch postfix itself).
            var st = new StackTrace(fNeedFileInfo: false);
            var count = Math.Min(frames + 2, st.FrameCount);
            for (int i = 2; i < count; i++)
            {
                var m = st.GetFrame(i)?.GetMethod();
                if (m == null) continue;
                if (sb.Length > 0) sb.Append(" < ");
                sb.Append(m.DeclaringType?.Name ?? "?").Append('.').Append(m.Name);
            }
            Trace(evt, "callers: " + (sb.Length > 0 ? sb.ToString() : "<none>"));
        }
        catch { Trace(evt, "callers: <unavailable>"); }
    }
}
