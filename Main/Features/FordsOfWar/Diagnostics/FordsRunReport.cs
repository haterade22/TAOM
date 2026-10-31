using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>
/// The one service every Fords seam calls to say what the engine did. It keeps the evidence for the current run (replaced at
/// each BeginRun), writes the event lines to the live log and, at a few moments, a verdict pass and a JSON report.
/// Cold members never throw (one WARNING per member, then quiet); the hot ones are written throw-free. <c>_sync</c> is a leaf
/// lock: nothing is called out while it is held. <see cref="TowerBlow"/> and <see cref="VolleyHit"/> may run on any thread.
/// </summary>
internal sealed partial class FordsRunReport
{
    private const int ConsoleCap = 20;
    private const int FlowCap = 20;
    private const int StageCap = 40;
    private const int MissingCap = 50;
    private const int HeroCap = 200;
    private const int HitCap = 200;
    private const int WindowCap = 120;
    private const double WindowSeconds = 30.0;
    private const int TowerSampleCap = 10;
    private const int VolleySampleCap = 10;
    private const int LaneSampleCap = 30;
    private const int PauseSampleCap = 20;

    private static readonly int[] Caps = { TowerSampleCap, VolleySampleCap, LaneSampleCap, PauseSampleCap };

    private readonly object _sync = new();
    private readonly HashSet<string> _faulted = new();
    private readonly Func<double> _now;
    private readonly FrameStats _frames = new(WindowSeconds, 8192);

    private double? _wallZero;
    private FordsRunEvidence? _run;
    private FordsReportFile? _file;
    private FordsConsoleRecord? _bound;
    private int _parkedRun;
    private string _parkedSetup = "";
    private double _windowSeconds;
    private double _lastFieldMt = -1.0;
    private int _liveWest;
    private int _liveEast;
    private int _backlog;

    public FordsRunReport(IModLogger logger)
        : this(logger, SystemClock())
    {
    }

    /// <summary>The seam a scenario test uses to drive the wall clock; <paramref name="wallSeconds"/> returns seconds on any fixed origin.</summary>
    internal FordsRunReport(IModLogger logger, Func<double> wallSeconds)
    {
        Logger = logger;
        _now = wallSeconds;
    }

    internal IModLogger Logger { get; }

    internal FordsSessionEvidence Session { get; } = new();

    /// <summary>The current run, null before the first BeginRun.</summary>
    internal FordsRunEvidence? Run => _run;

    /// <summary>Mission seconds of the current run; 0 before its first mission tick.</summary>
    internal double MissionSeconds => _run?.Mt ?? 0.0;

    private double Wall => _wallZero is { } zero ? _now() - zero : 0.0;

    private static Func<double> SystemClock()
    {
        var watch = Stopwatch.StartNew();
        return () => watch.Elapsed.TotalSeconds;
    }

    private void Guard(string member, Action body)
    {
        try
        {
            body();
        }
        catch (Exception ex)
        {
            bool first;
            lock (_faulted)
                first = _faulted.Add(member);
            if (first)
                Logger.LogWarning("[FordsOfWar][Report] " + member + " faulted and will stay quiet: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private string Line(string topic, string body) =>
        FordsLogLine.Of(topic, _run?.Run ?? 0, Wall, MissionSeconds, body);

    private void Info(string topic, string body) => Logger.LogInfo(Line(topic, body));

    private void Warn(string topic, string body) => Logger.LogWarning(Line(topic, body));

    private void Trace(string topic, string body) => Logger.LogDebug(Line(topic, body));

    /// <summary>Counts one event of a capped category. Returns its ordinal; the caller logs it when it is within the cap.</summary>
    private int NextOrdinal(FordsRunEvidence run, FordsLogCategory category) => ++run.Logged[(int)category];

    private void CappedLine(FordsLogCategory category, int ordinal)
    {
        if (ordinal == Caps[(int)category] + 1)
            Info("Log", "category=" + category + " capped after " + Caps[(int)category] + "; counting only");
    }

    private static string Informational()
    {
        var attribute = typeof(FordsRunReport).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return attribute?.InformationalVersion ?? "unknown";
    }
}
