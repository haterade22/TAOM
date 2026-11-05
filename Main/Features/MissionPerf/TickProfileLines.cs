using System;
using System.Globalization;

namespace TAOM.Features.MissionPerf;

/// <summary>
/// The status lines of the soft-failing call-site swap (<see cref="TickProfilerTranspiler"/>), trimmed from the
/// 1.5.x line's TickProfileLines to what that swap writes. WP117 (the tick profiler port) replaces this file with
/// the full one. Status lines carry <see cref="StatusTag"/>, and an exception message is quoted with its square
/// brackets turned into parentheses.
/// </summary>
public static class TickProfileLines
{
    public const string StatusTag = "[TickProfiler]";

    public static string BuildSiteCountWarning(string method, string target, int count, int expected = 1) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} {1}: {2} matched {3} times, expected {4}; {5}",
            StatusTag, method, target, count, expected, LeftVanilla(method));

    public static string BuildHelperMismatchWarning(string method, string helper, string target) =>
        StatusTag + " " + method + ": helper " + helper + " does not fit " + target + "; " + LeftVanilla(method);

    /// <summary>A transpiler that threw: its method stays vanilla, with the consequence for the profiler.</summary>
    public static string BuildRewriteFault(string method, Exception ex) =>
        StatusTag + " " + method + " rewrite failed: " + ex.GetType().Name + ": " + Quote(ex.Message)
        + "; " + LeftVanilla(method);

    private static string LeftVanilla(string method) => method + " left vanilla, so " + method switch
    {
        "Mission.OnTick" => "no mission times behaviours by type (with 'Enable Hitch Probe' on, missions still measure in probe mode)",
        "Mission.OnPreTick" => "preTickMs reads 0 and that time lands in otherMs (the hitch probe still times the wait)",
        _ => "per-type attribution records nothing for it (the hitch probe's totals stay)",
    };

    internal static string Quote(string? message) =>
        (message ?? string.Empty).Replace('[', '(').Replace(']', ')').Replace('\r', ' ').Replace('\n', ' ');
}
